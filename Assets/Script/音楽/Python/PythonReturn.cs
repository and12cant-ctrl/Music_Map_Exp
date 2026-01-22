using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

public struct PyRunResult
{
    public int ExitCode;
    public string Stdout;
    public string Stderr;
    public bool TimedOut;
}

public static class PythonReturn
{

    public static PyRunResult Run(
    string exePath,
    string arguments,
    int timeoutMs = 120_000,
    Encoding? encoding = null
)
    {
        return RunCore(exePath, arguments, stdinText: null, timeoutMs, encoding);
    }
    /// <summary>
    /// 任意の実行ファイル(ex: micromamba.exe)を起動し、
    /// stdin に文字列を渡し、stdout/stderr を回収して返す。
    /// </summary>
    public static PyRunResult RunWithStdin(
        string exePath,
        string arguments,
        string stdinText,
        int timeoutMs = 120_000,       // 2分（必要なら変更）
        Encoding encoding = null
    )
    {
        return RunCore(exePath, arguments, stdinText, timeoutMs, encoding);
    }

    private static PyRunResult RunCore(
         string exePath,
        string arguments,
        string? stdinText,
        int timeoutMs,
        Encoding? encoding
        )
    {
        encoding ??= Encoding.UTF8;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,

            UseShellExecute = false,
            RedirectStandardInput = stdinText != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,

            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();

            // stdin を送る（送ったら閉じるのが重要）
            if (!string.IsNullOrEmpty(stdinText))
            {
                proc.StandardInput.WriteLine(stdinText);
                proc.StandardInput.Close();
            }

            // stdout/stderr を最後まで読む（同期版）
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();

            bool exited = proc.WaitForExit(timeoutMs);
            if (!exited)
            {
                try { proc.Kill(); } catch { /* ignore */ }
                return new PyRunResult
                {
                    ExitCode = -1,
                    Stdout = stdout ?? "",
                    Stderr = (stderr ?? "") + "\n[PyRunner] Timed out.",
                    TimedOut = true
                };
            }

            return new PyRunResult
            {
                ExitCode = proc.ExitCode,
                Stdout = stdout ?? "",
                Stderr = stderr ?? "",
                TimedOut = false
            };
        }
        catch (Exception ex)
        {
            return new PyRunResult
            {
                ExitCode = -2,
                TimedOut = false,
                Stdout = "",
                Stderr = ex.ToString()
            };
        }
    }

    public static class PyLog
    {
        /// <summary>
        /// Python stderr を Unity Console 向けに整形して出す。
        /// - 行単位に分割
        /// - 空行除去
        /// - 先頭N行だけ/末尾N行だけ表示も可能（ログ洪水対策）
        /// - "Traceback" があれば Error で出す
        /// </summary>
        public static void LogStderr(
            string stderr,
            string contextTag = "PY",
            string contextInfo = null, // 例: midiPath など
            int maxLines = 80,
            bool showHeadAndTail = true,  // true: 先頭+末尾 / false: 先頭のみ
            int tailLines = 25
        )
        {
            if (string.IsNullOrWhiteSpace(stderr))
                return;

            var lines = stderr
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                return;

            bool hasTraceback = lines.Any(l => l.StartsWith("Traceback", StringComparison.OrdinalIgnoreCase));
            bool hasErrorWord = lines.Any(l => l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0);

            // 表示行の間引き（ログ洪水対策）
            var sb = new StringBuilder();
            sb.Append($"[{contextTag} STDERR]");
            if (!string.IsNullOrEmpty(contextInfo))
                sb.Append($" ({contextInfo})");
            sb.Append('\n');

            if (lines.Count <= maxLines)
            {
                foreach (var l in lines) sb.AppendLine(l);
            }
            else
            {
                if (showHeadAndTail)
                {
                    int headLines = Math.Max(1, maxLines - tailLines - 2); // 2行は省略表示用
                    headLines = Math.Min(headLines, lines.Count);

                    for (int i = 0; i < headLines; i++) sb.AppendLine(lines[i]);

                    sb.AppendLine($"... ({lines.Count - headLines - tailLines} lines omitted) ...");

                    int startTail = Math.Max(headLines, lines.Count - tailLines);
                    for (int i = startTail; i < lines.Count; i++) sb.AppendLine(lines[i]);
                }
                else
                {
                    for (int i = 0; i < maxLines; i++) sb.AppendLine(lines[i]);
                    sb.AppendLine($"... ({lines.Count - maxLines} lines omitted) ...");
                }
            }

            string msg = sb.ToString();

            // Traceback は Error 扱い（Unityで赤く出して追いやすくする）
            if (hasTraceback || hasErrorWord)
                UnityEngine.Debug.LogError(msg);
            else
                UnityEngine.Debug.LogWarning(msg);
        }

        /// <summary>
        /// stdoutも同様に見やすく（必要な時だけ）
        /// </summary>
        public static void LogStdout(string stdout, string contextTag = "PY", string contextInfo = null, int maxChars = 6000)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return;

            stdout = stdout.Trim();
            if (stdout.Length > maxChars)
                stdout = stdout.Substring(0, maxChars) + "\n... (truncated) ...";

            string header = $"[{contextTag} STDOUT]" + (string.IsNullOrEmpty(contextInfo) ? "" : $" ({contextInfo})");
            UnityEngine.Debug.Log($"{header}\n{stdout}");
        }
    }
}

