using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Midi_FulidSynth : MonoBehaviour
{
    [Header("Playback Options")]
    [Range(0f, 5f)]
    public float gain = 0.8f;           // 全体音量(0.0〜5.0)
    public bool loop = false;            // 曲をループ再生したい場合
    public string audioDriver = "dsound"; // Windows: dsound / wasapi（ビルドにより利用可）

    public string midi = "edited_score";
    public string MidiFile => Path.Combine(FluidSynthPaths.ScoresDir,midi);

    private Process _proc;
    private Coroutine _loopWatcher;
    public Button ButtonA;
    private void Start()
    {
        tton();
    }

    public void tton()
    {
        ButtonA.onClick.AddListener(() =>
        {
            UnityEngine.Debug.Log("uo");
            Play();
        });
    }
    public void Play()
    {
        Stop(); // 二重起動防止

        var args = $"-i -a {audioDriver} -g {gain} \"{FluidSynthPaths.SoundFontFile}\" \"{MidiFile}\"";
        var psi = new ProcessStartInfo
        {
            FileName =FluidSynthPaths.FluidSynthExe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.Log($"[fluidsynth] {e.Data}");
            };
            _proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogWarning($"[fluidsynth] {e.Data}");
            };
            if (_proc.Start())
            {
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                if (loop)
                {
                    _loopWatcher = StartCoroutine(WatchAndLoop());
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to start fluidsynth process.");
            }
            
            _proc.Start();

        }catch(Exception ex)
        {
            UnityEngine.Debug.LogError($"error fluidsynth{ex}");
        }
    }

    public void Stop()
    {
        if (_loopWatcher != null)
        {
            StopCoroutine(_loopWatcher);
            _loopWatcher = null;
        }

        if (_proc != null && !_proc.HasExited)
        {
            try
            {
                _proc.Kill();
            }
            catch { /* ignore */ }
        }

        if (_proc != null)
        {
            _proc.Dispose();
            _proc = null;
        }
    }

    public void SetMidiAndPlay(string newMidiRelativePath)
    {
        midi = newMidiRelativePath;
        Play();
    }

    public void SetGain(float newGain)
    {
        gain = Mathf.Clamp(newGain, 0f, 5f);
        // 反映には再起動が必要（外部プロセスのため）
        if (_proc != null && !_proc.HasExited)
        {
            Play();
        }
    }
    IEnumerator WatchAndLoop()
    {
        while (true)
        {
            if (_proc == null) yield break;
            if (_proc.HasExited)
            {
                // 再生終了 → 再起動でループ
                Play();
                yield break; // 再起動側で新しく WatchAndLoop が張られる
            }
            yield return new WaitForSeconds(0.25f);
        }
    }
}
