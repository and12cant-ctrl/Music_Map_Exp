using System;
using System.IO;
using System.Linq;
using System.Text;

public static class emotion_common
{
    /// <summary>
    /// emotion_common.py (CLI化済み) を呼び出して
    /// CSVの指定行(row)から感情推論JSONを返す。
    /// row: 0..N-1, -1なら最終行
    /// </summary>
    public static string InferFromCsvRow(
        string csvPath,
        int row,
        int timeoutMs = 120_000
    )
    {
        string emoCommonPy = Path.Combine(FluidSynthPaths.scriptPy, "emotion_common.py");
        string mambaRoot = FluidSynthPaths.micromambaroot;
        string mambaExe = FluidSynthPaths.micromambaExe;

        string modelPath = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm.txt");
        string metaPath = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm_meta.json");

        // micromamba run -r ... -n unitypy python -u emotion_common.py --csv ... --row ... --model ... --meta ...
        string args =
            $"run -r \"{mambaRoot}\" -n unitypy " +
            $"python -u \"{emoCommonPy}\" " +
            $"--csv \"{csvPath}\" --row {row} " +
            $"--model \"{modelPath}\" --meta \"{metaPath}\"";

        var res = PythonReturn.Run(
            exePath: mambaExe,
            arguments: args,
            timeoutMs: timeoutMs,
            encoding: Encoding.UTF8
        );


        PythonReturn.PyLog.LogStderr(res.Stderr, contextTag: "PY emo", contextInfo: csvPath);

        if (res.TimedOut)
            throw new Exception($"Emotion inference timed out.\n{res.Stderr}");

        if (res.ExitCode != 0)
            throw new Exception($"Emotion inference failed. ExitCode={res.ExitCode}\n{res.Stderr}");

        string json = ExtractLastNonEmptyLine(res.Stdout);
        if (string.IsNullOrWhiteSpace(json))
            throw new Exception("No JSON returned from python (stdout empty).");

        return json;
    }

    private static string ExtractLastNonEmptyLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var lines = s.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        return (lines.Count > 0) ? lines[^1] : "";
    }
}
