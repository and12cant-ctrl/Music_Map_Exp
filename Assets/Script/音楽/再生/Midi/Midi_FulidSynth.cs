using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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
    public void Play()
    {
        if (_isRunning) return;
        _isRunning = true;
        try
        {
            var daemon = MidiDaemonClient.Instance;
            if (daemon == null || !daemon.IsRunning)
            {
                UnityEngine.Debug.LogError("MidiDaemonClient is not running.");
                return;
            }
            if (!File.Exists(MidiFile))
            {
                UnityEngine.Debug.LogError("MIDI file was not found: " + MidiFile);
                return;
            }
            daemon.PlayMid(MidiFile, "legacy_fluid_synth");
        }
        finally
        {
            _isRunning = false;       
            playButton.interactable = true;
        }
    }

    public void Stop()
    {
        MidiDaemonClient.Instance?.StopMid();
        _isRunning = false;
    }

    public void SetMidiAndPlay(string newMidiRelativePath)
    {
        midi = newMidiRelativePath;
        Play();
    }

    public void SetGain(float newGain)
    {
        gain = Mathf.Clamp(newGain, 0f, 5f);
    }
}
