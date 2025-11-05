using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public class MidiDaemonClient : MonoBehaviour
{
    [Header("Audio / Synth Settings")]
    public string driver = "dsound"; // Windows: dsound / wasapi / winmm
    public int samplerate = 44100;
    public float gain = 0.5f;
    public string soundFontRelative = "Arachno.sf2"; 

    private Process proc;
    private StreamWriter stdin;

    void Start()
    {
        // ルート/各種パスの自動解決
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string streaming = Application.streamingAssetsPath;
        UnityEngine.Debug.Log(streaming);
        string micromambaExe = Path.Combine(root, "Tools/micromamba/micromamba.exe");
        string mambaRoot = Path.Combine(root, "Tools/micromamba/mamba_root");
        string envPrefix = Path.Combine(mambaRoot, "envs/unitypy"); // -p 用
        string daemonPyPath = Path.Combine(root, "ScriptsPy/midi_player_daemon.py");
        string dllDir = Path.Combine(root, "Tools/fluidsynth/bin");
        string sf2Abs = Path.Combine(streaming, soundFontRelative);

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

        proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.Log("[MIDI] " + e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogError("[MIDI-ERR] " + e.Data); };

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
        SendJson($"{{\"type\":\"play_mid\",\"id\":\"{id}\",\"path\":\"{Escape(absMidPath)}\"}}");
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
    }

    private void SendJson(string json)
    {
        if (stdin == null) { UnityEngine.Debug.LogError("[MIDI] stdin is null"); return; }
        stdin.WriteLine(json);
        stdin.Flush();
    }

    private static string Escape(string path)
    {
        return path.Replace("\\", "/");
    }
}
