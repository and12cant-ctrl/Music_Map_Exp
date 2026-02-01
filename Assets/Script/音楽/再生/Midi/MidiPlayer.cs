using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Linq;
using UnityEngine;
using System.IO;

public class MidiPlayer : MonoBehaviour
{

    public string midFileName = "Assets/StreamingAssets/scores/edited_score";

    public float startSeconds = 0f;
    public float volume = 1f;
    public bool loop = true;

    private OutputDevice _device;
    private Playback _playback;
    private MidiFile _midi;
    private void Start()
    {
        MidiStart();
    }

    public void MidiStart()
    {
        #region         Midi読み込み
        string path =Path.GetFullPath(Path.Combine((Directory.GetParent(Application.dataPath).FullName), midFileName));
        Debug.Log(path);
        if (!File. Exists(path))
        {
            Debug.LogError($"MIDI が見つかりません: {path}");
            return;
        }
        _midi = MidiFile.Read(path);
        #endregion
        #region 出力デバイス確認
        _device = GetWindowsSynthOrFirst();
        if (_device == null)
        {
            Debug.LogError("MIDI 出力デバイスがありません。");
            return;
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
            _playback.Stop();
        }
    }
    private OutputDevice GetWindowsSynthOrFirst()
    {
        // "Microsoft GS Wavetable Synth" を優先
        foreach (var info in OutputDevice.GetAll())
        {
            if (info.Name.ToLower().Contains("microsoft gs wavetable"))
                return OutputDevice.GetByName(info.Name);
        }

        // 見つからない場合は先頭
        var all = OutputDevice.GetAll();
        var first = all.FirstOrDefault();
        return first != null ? OutputDevice.GetByName(first.Name) : null;
    }
    public void OnDestroy()
    {
        if (_playback != null)
        {
            try { _playback.Stop(); } catch { }
            _playback.Finished -= OnPlaybackFinished;
            _playback.Dispose();
            _playback = null;
        }
        _device?.Dispose();
        _device = null;
        _midi = null;
    }
}

