using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Threading.Tasks;

public class Midi_FulidSynth : MonoBehaviour
{
    [Header("Playback Options")]
    [Range(0f, 5f)]
    public float gain = 0.8f;           // 全体音量(0.0〜5.0)
    public bool loop = false;            // 曲をループ再生したい場合
    public string audioDriver = "dsound"; // Windows: dsound / wasapi（ビルドにより利用可）

    public string midi = "edited_score.mid";
    public string MidiFile => Path.Combine(FluidSynthPaths.ScoresDir,midi);
    public string Pyscripts => Path.Combine(FluidSynthPaths.scriptPy,"midi_fluidsynth.py");

    private Process _proc;
    private Coroutine _loopWatcher;
    public Button playButton;
    private bool _isRunning;
    private MusicXmlEditorCaller xmlChenge;
    private Xml_to_MIDI midiChenge;
    private void Start()
    {
        xmlChenge = GetComponent<MusicXmlEditorCaller>();
        midiChenge = GetComponent<Xml_to_MIDI>();
        tton();

    }

    public void tton()
    {
        playButton.onClick.AddListener(() =>
        {
            xmlChenge.Score_Xml_MIDI();
            midiChenge.RunVeny();
            Play();
        });
    }
    public async void Play()
    {
        if (_isRunning) return;
        _isRunning = true;
        try
        {

            var args = $"run -r \"{FluidSynthPaths.micromambaroot}\" -n unitypy python \"{Pyscripts}\" --root \"{FluidSynthPaths.ProjectRoot}\"";
            var psi = new ProcessStartInfo
            {
                FileName = FluidSynthPaths.micromambaExe,
                Arguments = args,
                WorkingDirectory = FluidSynthPaths.ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.EnvironmentVariables["PROJECT_ROOT"] = FluidSynthPaths.ProjectRoot;
            psi.EnvironmentVariables["MIDI_FILENAME"] = MidiFile;

            var proc = new Process();
            proc.StartInfo = psi;
            proc.EnableRaisingEvents = true;

            #region 結果出力
            proc.OutputDataReceived += (s, e) =>
            {
                //if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.Log($"[PYTHON OUT] {e.Data}");
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.LogWarning($"[PYTHON ERR] {e.Data}");
            };

            proc.Exited += (s, e) =>
            {
                //UnityEngine.Debug.Log($"[PYTHON EXIT] code={proc.ExitCode}");
                proc.Dispose();
            };

            #endregion

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await Task.Run(() => proc.WaitForExit());
            //UnityEngine.Debug.Log($"[PY EXIT] code={proc.ExitCode}");

        }
        finally
        {
            _isRunning = false;       
            playButton.interactable = true;
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
