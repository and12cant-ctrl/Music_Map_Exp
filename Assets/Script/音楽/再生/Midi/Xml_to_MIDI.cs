using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        string exeAbs =Path.GetFullPath(Path.Combine(projectRoot,micromambaExe));
        string rootAbs =Path.GetFullPath( Path.Combine(projectRoot,mambaRootDir));
        string scriptAbs = Path.GetFullPath(Path.Combine(projectRoot,scriptPath));
        string inAbs = Path.GetFullPath(Path.Combine(projectRoot, inputXml));
        string outAbs = Path.GetFullPath(Path.Combine(projectRoot , outputMid));

        if (!File.Exists(exeAbs)) {UnityEngine.Debug.LogError($"[convert] micromamba.exe が見つかりません: {exeAbs}"); return; }
        if (!Directory.Exists(rootAbs)) { UnityEngine.Debug.LogError($"[convert] mamba_root が見つかりません。先にブートストラップしてください: {rootAbs}"); return; }
        if (!File.Exists(scriptAbs)) { UnityEngine.Debug.LogError($"[convert] スクリプトが見つかりません: {scriptAbs}"); return; }
        if (!File.Exists(inAbs)) { UnityEngine.Debug.LogError($"[convert] 入力XMLが見つかりません: {inAbs}"); return; }

        #endregion

        #region micromambaに渡す文字列を構築

        var argsBuilder = new StringBuilder();
        argsBuilder.Append($"run -r \"{rootAbs}\" -n {envName} python \"{scriptAbs}\" \"{inAbs}\"");
        if (!string.IsNullOrWhiteSpace(outAbs)) argsBuilder.Append($" \"{outAbs}\"");
        string args = argsBuilder.ToString();
        #endregion

        var psi = new ProcessStartInfo
        {
            FileName = exeAbs,
            Arguments = args,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var p = Process.Start(psi);

            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();
            if (p == null) { UnityEngine.Debug.LogError("[convert] プロセス起動に失敗"); return; }
            
            bool exited;
            if (waitTimeoutMs > 0)
            {
                exited = p.WaitForExit(waitTimeoutMs);
            }
            else
            {
                p.WaitForExit();
                exited = true;
            }

            if (!exited)
            {
                try { p.Kill(); } catch { }
                UnityEngine.Debug.LogError($"[convert] 失敗 (ExitCode={p.ExitCode})\n--- STDERR ---\n{stdErr}\n--- STDOUT ---\n{stdOut}");
                return;
            }

            if (p.ExitCode != 0)
                UnityEngine.Debug.Log($"[convert]失敗:\n{stdErr}\n{stdOut}");

            string expectedOut = outAbs;
            if (string.IsNullOrWhiteSpace(expectedOut))
            {
                var inferred = Path.ChangeExtension(inAbs, ".mid");
                expectedOut = inferred;
            }
            //if (File.Exists(expectedOut)) UnityEngine.Debug.Log($"[convert] 出力を確認: {expectedOut}");
            //else UnityEngine.Debug.LogWarning($"[convert] ExitCode=0 ですが出力MIDIが見つかりません: {expectedOut}");
        }
        catch (Exception e)
        {
           UnityEngine.Debug.LogError($"[venv] 例外: {e.GetType().Name}\n{e.Message}\n{e.StackTrace}");
        }
    }


}
