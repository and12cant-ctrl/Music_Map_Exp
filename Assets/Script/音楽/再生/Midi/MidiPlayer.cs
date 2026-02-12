using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Linq;
using UnityEngine;
using System.IO;

public class MidiPlayer : MonoBehaviour
{

    public string midFileName = "Assets/StreamingAssets/scores/edited_score_MAIN.mid";

    public float startSeconds = 0f;
    public float volume = 1f;
    public bool loop = true;

    private OutputDevice _device;
    private Playback _playback;
    private MidiFile _midi;
    private void Start()
    {
        //MidiStart();
    }

    public void MidiStart()
    {
        StopInternal();
        #region         Midi読み込み
        string path =Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, midFileName));
        if (!File. Exists(path))
        {
            Debug.LogError($"MIDI が見つかりません: {path}");
            return;
        }
        _midi = MidiFile.Read(path);
        #endregion
        #region 出力デバイス確認
        if (_device == null)
        {
            var name = GetWindowsSynthOrFirstName();
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("MIDI 出力デバイスがありません。");
                return;
            }
            _device = OutputDevice.GetByName(name);
        }
        #endregion

        _playback = _midi.GetPlayback(_device);

        _playback.Finished += OnPlaybackFinished;

        // 開始位置（秒）へ
        if (startSeconds > 0f)
            _playback.MoveToTime(new MetricTimeSpan(0,0,(int)startSeconds));

        // 4) 再生
        _playback.Start();
    }
    private void OnPlaybackFinished(object sender, EventArgs e)
    {
        if (_playback == null) return;

        if (loop)
        {
            _playback.MoveToStart();
            _playback.Start();
        }
        else
        {
            // 末尾で停止
            StopInternal();
        }
    }

    public void OnDestroy()
    {
        StopInternal();

        _device?.Dispose();
        _device = null;
        _midi = null;
    }
    private void StopInternal()
    {
        if (_playback != null)
        {
            try { if (_playback.IsRunning) _playback.Stop(); } catch { }
            try { _playback.Finished -= OnPlaybackFinished; } catch { }
            try { _playback.Dispose(); } catch { }
            _playback = null;
        }
    }
    private string GetWindowsSynthOrFirstName()
    {
        foreach (var info in OutputDevice.GetAll())
        {
            if (info.Name.ToLower().Contains("microsoft gs wavetable"))
                return info.Name;
        }

        var first = OutputDevice.GetAll().FirstOrDefault();
        return first?.Name;
    }
}

