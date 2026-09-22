using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class MidiDaemonClient : MonoBehaviour
{
    [Serializable]
    private sealed class ScriptCommand
    {
        public string type = "run_script";
        public string request_id;
        public string script;
        public string[] args;
        public string stdin;
    }

    [Serializable]
    private sealed class DaemonResponse
    {
        public string @event;
        public string request_id;
        public int exit_code;
        public string stdout;
        public string stderr;
        public bool ready;
        public string error;
        public string detail;
    }

    public static MidiDaemonClient Instance { get; private set; }

    [Header("Audio / Synth Settings")]
    public string driver = "dsound"; // Windows: dsound / wasapi / winmm
    public int samplerate = 44100;
    public float gain = 0.5f;
    public string soundFontRelative = "Arachno.sf2"; 

    private Process proc;
    private StreamWriter stdin;
    private readonly object stdinLock = new object();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PyRunResult>> pendingScripts = new();
    private volatile bool isInitialized;
    private volatile string initializationError;

    public bool IsRunning => proc != null && !proc.HasExited && stdin != null;
    public bool IsInitialized => IsRunning && isInitialized;
    public string InitializationError => initializationError;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            UnityEngine.Debug.LogWarning("[PythonDaemon] Multiple MidiDaemonClient instances were found.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    void Start()
    {
        isInitialized = false;
        initializationError = null;

        // ルート/各種パスの自動解決
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string streaming = Application.streamingAssetsPath;
        UnityEngine.Debug.Log(streaming);
        string micromambaExe = Path.Combine(root, "Tools/micromamba/micromamba.exe");
        string mambaRoot = Path.Combine(root, "Tools/micromamba/mamba_root");
        string envPrefix = Path.Combine(mambaRoot, "envs/unitypy");     // -p 用
        string daemonPyPath = Path.Combine(root, "ScriptsPy/midi_player_daemon.py");
        string dllDir = FluidSynthPaths.FluidSynthDir;
        string sf2Abs = FluidSynthPaths.SoundFontFile;

        #region 前提チェック
        if (!File.Exists(micromambaExe)) { UnityEngine.Debug.LogError("[MIDI] micromamba.exe NOT FOUND: " + micromambaExe); return; }
        if (!Directory.Exists(envPrefix)) { UnityEngine.Debug.LogError("[MIDI] env prefix NOT FOUND: " + envPrefix); return; }
        if (!File.Exists(daemonPyPath)) { UnityEngine.Debug.LogError("[MIDI] daemonPyPath NOT FOUND: " + daemonPyPath); return; }
        if (!Directory.Exists(Path.GetDirectoryName(sf2Abs))) { UnityEngine.Debug.LogError("[MIDI] StreamingAssets missing: " + streaming); return; }
        if (!File.Exists(sf2Abs)) { UnityEngine.Debug.LogError("[MIDI] SoundFont NOT FOUND: " + sf2Abs); return; }
        if (!Directory.Exists(dllDir)) { UnityEngine.Debug.LogWarning("[MIDI] fluidsynth dllDir missing: " + dllDir + " (続行は試みます)"); }

        #endregion

        // micromamba run -p <prefix> python <daemon>
        string args = $"run -p \"{envPrefix}\" python \"{daemonPyPath}\"";

        proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = micromambaExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        // ★ FluidSynth の DLL を見せる：PATH を先頭上書き
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        proc.StartInfo.EnvironmentVariables["PATH"] = dllDir + ";" + currentPath;
        proc.StartInfo.EnvironmentVariables["MAMBA_ROOT_PREFIX"] = mambaRoot;

        proc.OutputDataReceived += (s, e) => HandleStdout(e.Data);
        proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogError("[MIDI-ERR] " + e.Data); };
        proc.Exited += (s, e) =>
        {
            isInitialized = false;
            initializationError = "Python daemon exited unexpectedly.";
            FailPendingScripts(initializationError);
        };

        if (!proc.Start()) { UnityEngine.Debug.LogError("Failed to start MIDI daemon."); return; }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        stdin = proc.StandardInput;

        // init 送信：dll_dir を渡す（遅延 import 前提）
        SendJson($"{{\"type\":\"init\",\"sf2\":\"{Escape(sf2Abs)}\",\"samplerate\":{samplerate},\"driver\":\"{driver}\",\"gain\":{gain.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"dll_dir\":\"{Escape(dllDir)}\"}}");

        // 動作確認用にログ
        UnityEngine.Debug.Log("[MIDI] Launched daemon with -p prefix: " + envPrefix);
    }

    public void PlayMid(string absMidPath, string id = "bgm")
    {
        if (!IsRunning)
        {
            UnityEngine.Debug.LogError("[MIDI] Pythonデーモンが起動していません。");
            return;
        }

        if (!IsInitialized)
        {
            string detail = string.IsNullOrEmpty(initializationError)
                ? "FluidSynthの初期化完了を待っています。"
                : initializationError;
            UnityEngine.Debug.LogError("[MIDI] 再生できません。" + detail);
            return;
        }

        SendJson($"{{\"type\":\"play_mid\",\"id\":\"{id}\",\"path\":\"{Escape(absMidPath)}\"}}");
    }

    public void StopMid()
    {
        SendJson("{\"type\":\"stop_mid\"}");
    }

    public PyRunResult RunPythonScript(
        string scriptPath,
        string[] arguments = null,
        string stdinText = null,
        int timeoutMs = 120_000)
    {
        if (!IsRunning)
            return FailedResult("Python daemon is not running.");

        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            return FailedResult("Python script was not found: " + scriptPath);

        string requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<PyRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pendingScripts.TryAdd(requestId, completion))
            return FailedResult("Failed to register Python request: " + requestId);

        var command = new ScriptCommand
        {
            request_id = requestId,
            script = Path.GetFullPath(scriptPath).Replace("\\", "/"),
            args = arguments ?? Array.Empty<string>(),
            stdin = stdinText ?? string.Empty,
        };

        if (!SendJson(JsonUtility.ToJson(command)))
        {
            pendingScripts.TryRemove(requestId, out _);
            return FailedResult("Failed to send a request to the Python daemon.");
        }

        if (!completion.Task.Wait(timeoutMs))
        {
            pendingScripts.TryRemove(requestId, out _);
            return new PyRunResult
            {
                ExitCode = -1,
                Stdout = string.Empty,
                Stderr = $"[PythonDaemon] Timed out after {timeoutMs} ms. request={requestId}",
                TimedOut = true,
            };
        }

        return completion.Task.Result;
    }

    public void Note(int ch, int note, int vel = 100)
    {
        SendJson($"{{\"type\":\"note\",\"ch\":{ch},\"note\":{note},\"vel\":{vel}}}");
    }

    public void NoteOff(int ch, int note)
    {
        SendJson($"{{\"type\":\"note_off\",\"ch\":{ch},\"note\":{note}}}");
    }

    public void Program(int ch, int program, bool drum = false)
    {
        SendJson($"{{\"type\":\"program\",\"ch\":{ch},\"program\":{program},\"drum\":{drum.ToString().ToLower()} }}");
    }

    void OnDestroy()
    {
        try
        {
            if (stdin != null)
            {
                SendJson("{\"type\":\"shutdown\"}");
                stdin.Flush();
            }
            if (proc != null && !proc.HasExited)
            {
                proc.WaitForExit(1000);
                if (!proc.HasExited) proc.Kill();
            }
        }
        catch { }
        finally
        {
            isInitialized = false;
            FailPendingScripts("Python daemon was destroyed.");
            if (Instance == this) Instance = null;
            stdin?.Dispose();
            proc?.Dispose();
        }
    }

    private void HandleStdout(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        try
        {
            var response = JsonUtility.FromJson<DaemonResponse>(line);
            if (response != null && response.ready)
            {
                isInitialized = true;
                initializationError = null;
                UnityEngine.Debug.Log("[MIDI] FluidSynthの初期化が完了しました。");
                return;
            }

            if (response != null && !string.IsNullOrEmpty(response.error))
            {
                if (response.error == "init failed" ||
                    response.error == "import fluidsynth failed" ||
                    response.error == "sf2 not found" ||
                    response.error == "not initialized")
                {
                    isInitialized = false;
                    initializationError = string.IsNullOrEmpty(response.detail)
                        ? response.error
                        : response.error + ": " + response.detail;
                }

                UnityEngine.Debug.LogError("[MIDI] " + line);
                return;
            }

            if (response != null && response.@event == "script_result" &&
                !string.IsNullOrEmpty(response.request_id) &&
                pendingScripts.TryRemove(response.request_id, out var completion))
            {
                completion.TrySetResult(new PyRunResult
                {
                    ExitCode = response.exit_code,
                    Stdout = response.stdout ?? string.Empty,
                    Stderr = response.stderr ?? string.Empty,
                    TimedOut = false,
                });
                return;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("[PythonDaemon] Invalid response: " + ex.Message);
        }

        UnityEngine.Debug.Log("[MIDI] " + line);
    }

    private void FailPendingScripts(string message)
    {
        foreach (var entry in pendingScripts)
        {
            if (pendingScripts.TryRemove(entry.Key, out var completion))
                completion.TrySetResult(FailedResult(message));
        }
    }

    private static PyRunResult FailedResult(string message)
    {
        return new PyRunResult
        {
            ExitCode = -2,
            Stdout = string.Empty,
            Stderr = "[PythonDaemon] " + message,
            TimedOut = false,
        };
    }

    private bool SendJson(string json)
    {
        if (stdin == null) { UnityEngine.Debug.LogError("[MIDI] stdin is null"); return false; }
        try
        {
            lock (stdinLock)
            {
                stdin.WriteLine(json);
                stdin.Flush();
            }
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[PythonDaemon] send failed: " + ex.Message);
            return false;
        }
    }

    private static string Escape(string path)
    {
        return path.Replace("\\", "/");
    }
}
