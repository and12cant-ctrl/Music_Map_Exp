using System;
using System.IO;
using UnityEngine;

public class Xml_to_MIDI : MonoBehaviour
{
    [HideInInspector]
    private string projectRoot => FluidSynthPaths.ProjectRoot;
    public string micromambaExe => FluidSynthPaths.micromambaExe;
    public string mambaRootDir => FluidSynthPaths.micromambaroot;
    public string envName => "unitypy";
    public string scriptargs => "adwadwa ";
    public string scriptPath => Path.Combine(FluidSynthPaths.scriptPy, "Xml_to_Midi.py");
    public string inputXml => Path.Combine(FluidSynthPaths.ScoresDir, "edited_score_MAIN.xml");
    public string outputMid => Path.Combine(FluidSynthPaths.ScoresDir,  "edited_score_MAIN.mid");

    public bool projectRelativeForIO => true;  
    public bool logPaths => true;

    public int waitTimeoutMs => 30000;


    [ContextMenu("XML => MIDI(micromamba)")]
    public void RunVeny()
    {
        #region NullTest
        string scriptAbs = Path.GetFullPath(Path.Combine(projectRoot,scriptPath));
        string inAbs = Path.GetFullPath(Path.Combine(projectRoot, inputXml));
        string outAbs = Path.GetFullPath(Path.Combine(projectRoot , outputMid));

        if (!File.Exists(scriptAbs)) { UnityEngine.Debug.LogError($"[convert] スクリプトが見つかりません: {scriptAbs}"); return; }
        if (!File.Exists(inAbs)) { UnityEngine.Debug.LogError($"[convert] 入力XMLが見つかりません: {inAbs}"); return; }

        #endregion

        var daemon = MidiDaemonClient.Instance;
        if (daemon == null || !daemon.IsRunning)
        {
            UnityEngine.Debug.LogError("[convert] MidiDaemonClientが起動していません。");
            return;
        }

        var result = daemon.RunPythonScript(
            scriptAbs,
            new[] { inAbs, outAbs },
            timeoutMs: waitTimeoutMs > 0 ? waitTimeoutMs : 120_000);

        if (result.TimedOut || result.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"[convert] 失敗 (ExitCode={result.ExitCode})\n--- STDERR ---\n{result.Stderr}\n--- STDOUT ---\n{result.Stdout}");
            return;
        }

        if (!File.Exists(outAbs))
            UnityEngine.Debug.LogWarning($"[convert] ExitCode=0 ですが出力MIDIが見つかりません: {outAbs}");
    }


}
