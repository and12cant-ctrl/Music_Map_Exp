using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class MidiConverter
{
    /// <summary>
    /// FluidSynth で MIDI→WAV を変換（ブロックしない）。成功時 outWavPath を返す。
    /// </summary>
    public static async Task<string> ConvertMidiToWavAsync(
        string midiAbsPath,
        string soundFontAbsPath,
        string outWavAbsPath,
        int timeoutMs = 60_000,
        CancellationToken ct = default)
    {
        if (!File.Exists(FluidSynthPaths.FluidSynthExe))
            throw new FileNotFoundException("fluidsynth.exe が見つかりません。", FluidSynthPaths.FluidSynthExe);

        if (!File.Exists(midiAbsPath))
            throw new FileNotFoundException("MIDI が見つかりません。", midiAbsPath);

        if (!File.Exists(soundFontAbsPath))
            throw new FileNotFoundException("SoundFont(SF2) が見つかりません。", soundFontAbsPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outWavAbsPath)!);

        // 既存WAVは上書き（キャッシュ実装前の最小形）
        if (File.Exists(outWavAbsPath)) File.Delete(outWavAbsPath);

        var psi = new ProcessStartInfo
        {
            FileName = FluidSynthPaths.FluidSynthExe,
            // ※ 引数に空白が入っても安全なように必ず Quote
            Arguments = $"-F \"{outWavAbsPath}\" \"{soundFontAbsPath}\" \"{midiAbsPath}\"",
            WorkingDirectory = FluidSynthPaths.FluidSynthDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var tcs = new TaskCompletionSource<int>();
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        proc.Exited += (_, __) => tcs.TrySetResult(proc.ExitCode);

        if (!proc.Start())
            throw new Exception("fluidsynth の起動に失敗しました。");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using (ct.Register(() =>
        {
            try
            {
                if (!proc.HasExited) ProcUtil.KillSoft(proc);
            }
            catch { }
        }))
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, ct));
            if (completed != tcs.Task)
            {
                try
                {
                    if (!proc.HasExited) ProcUtil.KillSoft(proc);
                }
                catch { }
                throw new TimeoutException($"fluidsynth がタイムアウトしました（{timeoutMs} ms）。\nSTDERR:\n{stderr}");
            }
        }

        var exit = await tcs.Task;
        if (exit != 0 || !File.Exists(outWavAbsPath))
        {
            throw new Exception($"fluidsynth が異常終了しました。ExitCode={exit}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        return outWavAbsPath;
    }
    static class ProcUtil
    {
        public static void KillSoft(Process proc)
        {
            if (proc == null) return;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // 子プロセスごと強制終了（/T でツリー、/F で強制）
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {proc.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    })?.WaitForExit(3000);
                }
                else
                {
                    // 他OSはツリーKillの簡易手段がないため親だけKill
                    if (!proc.HasExited) proc.Kill();
                }
            }
            catch { /* 無視 */ }
        }
    }
}
