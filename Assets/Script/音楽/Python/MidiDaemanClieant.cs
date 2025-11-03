using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public class MidiDaemanClieant : MonoBehaviour
{
    // ゲーム実行フォルダを基準に絶対パスを解決


    [Header("Micromamba / Python 起動設定")]
    private string envName = "unitypy";

    [Header("SoundFont / Driver 設定")]
    public string soundFontRelative = "Arachno.sf2"; 
    public string driver = "dsound";
    public int samplerate = 44100;
    public float gain = 0.5f;

    private Process proc;
    private StreamWriter stdin;

    private void Start()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string streaming = Application.streamingAssetsPath;

        string micromambaExe = Path.Combine(root, "Tools/micromamba/micromamba.exe");
        string mambaRoot     = Path.Combine(root, "Tools/micromamba/mamba_root");
        string daemonPyPath = Path.Combine(root, "ScriptsPy\\midi_player_daemon.py");

        string sf2Abs = Path.Combine(streaming, "FluidR3_GM.sf2");
        // micromamba run -r <root> -n <env> python <daemon>

        string args = $"run -r \"{mambaRoot}\" -n {envName} python \"{daemonPyPath}\"";
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

        proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log("[MIDI] " + e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError("[MIDI-ERR] " + e.Data); };

        if (!proc.Start())
        {
            UnityEngine.Debug.LogError("Failed to start MIDI daemon.");
            return;
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        stdin = proc.StandardInput;
        SendJson($"{{\"type\":\"init\",\"sf2\":\"{Escape(sf2Abs)}\",\"samplerate\":{samplerate},\"driver\":\"{driver}\",\"gain\":{gain.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}");
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
        if (stdin == null)
        {
            UnityEngine.Debug.LogError("[MIDI] stdin is null");
            return;
        }
        stdin.WriteLine(json);
        stdin.Flush();
    }

        private static string Escape(string path)
    {
        return path.Replace("\\", "/");
    }
}
