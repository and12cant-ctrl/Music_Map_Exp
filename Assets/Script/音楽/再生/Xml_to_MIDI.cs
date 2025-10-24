using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public class Xml_to_MIDI : MonoBehaviour
{
 
    public string venvPythonPath = @"Tools\music21\.venv\Scripts\python.exe";
    public string scriptPath = @"Tools\music21\convert_xml_to_midi.py";

    public string scriptP = @"Tools\music21\test.py";
    public string scriptargs = "adwadwa ";

    public string inputXml = @"StreamingAssets/Scores/edited_score.xml";
    public string outputMid = @"StreamingAssets/Scores/edited_score.mid";

    public bool projectRelativeForIO = true;  // 入出力 path をプロジェクト相対で解決
    public bool logPaths = true;

    public int waitTimeoutMs = 30000;

    [ContextMenu("RunTest")]
    public void RunVeny()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string absPy = Path.GetFullPath(Path.Combine(projectRoot, venvPythonPath.Replace('\\', '/')));
        string absScr = Path.GetFullPath(Path.Combine(projectRoot, scriptP.Replace('\\', '/')));

        if (!File.Exists(absPy))
        {
            UnityEngine.Debug.LogError($"[venv] python.exe が見つかりません: {absPy}");
            return;
        }
        if (!File.Exists(absScr))
        {
            UnityEngine. Debug.LogError($"[venv] スクリプトが見つかりません: {absScr}");
            return;
        }

        string exe = venvPythonPath;
        string args = $"\"{scriptP}\"{scriptargs}".Trim();

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = projectRoot, // ★相対解決の基準を固定
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

            p.Start();
            p.WaitForExit();
            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();
            if (p == null) UnityEngine.Debug.LogError("venvプロセス失敗");
            bool exited;
            if (waitTimeoutMs > 0)
                exited = p.WaitForExit(waitTimeoutMs);
            else
            {
                p.WaitForExit();
                exited = true;
            }

            if (!exited)
            {
                try { p.Kill(); } catch { }
                UnityEngine.Debug.LogError("[venv] タイムアウトしました。");
                return;
            }
            if (p.ExitCode != 0)
                UnityEngine.Debug.Log($"[venv]失敗:\n{stdErr}\n{stdOut}");
            else
                UnityEngine.Debug.Log($"成功:\n{stdOut}");
        }
        catch (Exception e)
        {
           UnityEngine.Debug.LogError($"[venv] 例外: {e.GetType().Name}\n{e.Message}\n{e.StackTrace}");
        }
    }


}
