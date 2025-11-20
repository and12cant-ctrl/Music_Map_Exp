using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

public static class PythonVenvRebuilder
{
    private static string VenvPath =>
        Path.Combine(FluidSynthPaths.ToolsDir, "music21/.venv");

    private static string UnityPyPython =>
        Path.Combine(FluidSynthPaths.micromambaroot, "envs/unitypy/python.exe");

    private static string Requirements =>
        "music21 mido pyfluidsynth";

    [MenuItem("Tools/Rebuild Python venv")]
    public static void RebuildVenv()
    {
        // 1) venv を削除
        if (Directory.Exists(VenvPath))
        {
            Directory.Delete(VenvPath, true);
            UnityEngine.Debug.Log("[VENV] 旧仮想環境を削除しました");
        }

        // 2) unitypy環境で venv 作成
        string cmdCreate = $"-m venv \"{VenvPath}\"";
        RunPython(UnityPyPython, cmdCreate, "[VENV] venv 作成");

        // 3) ライブラリをインストール
        string pythonExe = Path.Combine(VenvPath, "Scripts/python.exe");

        string cmdInstall = $"-m pip install {Requirements}";
        RunPython(pythonExe, cmdInstall, "[VENV] ライブラリをインストールしました");

        UnityEngine.Debug.Log("[VENV] 完成しました！");
    }

    private static void RunPython(string pythonExe, string arguments, string label)
    {
        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = pythonExe,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi);
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        UnityEngine.Debug.Log($"{label} STDOUT:\n{stdout}");
        if (!string.IsNullOrWhiteSpace(stderr))
            UnityEngine.Debug.LogWarning($"{label} STDERR:\n{stderr}");
    }
}
