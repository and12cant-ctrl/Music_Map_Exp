using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.Text;
using System.IO;

public static class GenerateScores
{
    [MenuItem("Tools/Music21/空の楽譜を生成")]
    public static void RunMakeScore()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string toolRoot = Path.Combine(projectRoot, "Tools", "music21");
        string pyExe = Path.Combine(toolRoot, ".venv", "Scripts", "python.exe");
        string script = Path.Combine(toolRoot, "src", "make_score.py");

        #region NULLチェック
        if (!File.Exists(pyExe))
        {
            EditorUtility.DisplayDialog("Music21", $"python.exe が見つかりません:\n{pyExe}\n先に仮想環境を作ってください。", "OK");
            return;
        }
        if (!File.Exists(script))
        {
            EditorUtility.DisplayDialog("Music21", $"スクリプトが見つかりません:\n{script}", "OK");
            return;
        }
        #endregion

        var psi = new ProcessStartInfo
        {
            FileName = pyExe,
            Arguments = $"\"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = toolRoot
        };

        try
        {
            using var p = Process.Start(psi);
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                UnityEngine.Debug.LogError($"[music21] Error:\n{stderr}");
            else
                UnityEngine.Debug.Log($"[music21] OK:\n{stdout}");

            AssetDatabase.Refresh(); // 生成物を Unity に反映
            EditorUtility.DisplayDialog("Music21", "生成が完了しました。StreamingAssets/scores を確認してください。", "OK");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }
    }
}