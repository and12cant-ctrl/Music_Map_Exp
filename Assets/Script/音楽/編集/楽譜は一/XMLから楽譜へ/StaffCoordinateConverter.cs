using System;
using UnityEngine;

public sealed class StaffCoordinateConverter
{
    private readonly RectTransform _staffRect;
    private readonly float _beatsPerBar;
    private readonly float _pixelsPerBar;
    private readonly int _scaleSteps;


    public StaffCoordinateConverter(RectTransform staffRect, float beatsPerBar, float pixelsPerBar, int scaleSteps)
    {
        _staffRect = staffRect;
        _beatsPerBar = beatsPerBar;
        _pixelsPerBar = pixelsPerBar;
        _scaleSteps = Mathf.Max(1, scaleSteps);

    }

    public Vector2 ToAnchoredPosition(ScoreEvent e)
    {
        float x = (e.bar - 1) * _pixelsPerBar + (e.start / _beatsPerBar) * _pixelsPerBar;

        float ny = PitchToNormalizedY(e.pitch);
        float y = Mathf.Lerp(0, _staffRect.rect.height , ny);

        return new Vector2(x, y);
    }

    /// <summary>
    /// pitch("C#4","D♭5","A3") を 0..1 に変換し、scaleSteps段にスナップする。
    /// 範囲外はクランプ。
    /// </summary>
    public float PitchToNormalizedY(string pitch)
    {
        if (string.IsNullOrEmpty(pitch) || pitch == "REST")
            return Snap01(0.5f); // 休符は中央（好みで下に落としてもOK）

        int midi = PitchStringToMidi(pitch);

        // 範囲外はクランプ
        midi = Mathf.Clamp(midi, PitchRange.MinMidi, PitchRange.MaxMidi);

        float ny = Mathf.InverseLerp(PitchRange.MinMidi, PitchRange.MaxMidi, midi);

        // scaleStepsにスナップ（クリック側の「段」に合わせる）
        return Snap01(ny);
    }

    private float Snap01(float ny)
    {
        ny = Mathf.Clamp01(ny);

        // 0..1 を scaleSteps 分割したグリッドにスナップ
        float step = 1f / _scaleSteps;

        float snapped = Mathf.Round(ny / step) * step;
        Debug.Log($"step = {step},_scaleSteps = {_scaleSteps},snapped = {snapped}");
        //return Mathf.Clamp(snapped, 0f, 1f - step);
        return Mathf.Clamp(snapped, 0f, 1f);
    }

    /// <summary>
    /// "C#4", "D♭5", "A3" などをMIDI番号へ。解析できなければ例外。
    /// </summary>
    public static int PitchStringToMidi(string pitch)
    {
        // 末尾の数字（octave）を取る：例 "C#4" -> 4
        int octave = ExtractTrailingInt(pitch, out int cutIndex);

        // 残りが音名部分：例 "C#"
        string note = pitch.Substring(0, cutIndex);

        // ♭/# 対応（ASCIIに寄せる）
        note = note.Replace("♭", "b").Replace("-","b");

        int pc = note switch
        {
            "C" => 0,
            "C#" => 1,"Db" => 1,
            "D" => 2,
            "D#" => 3,"Eb" => 3,
            "E" => 4,"Fb" => 4,
            "E#" => 5,"F" => 5,
            "F#" => 6,"Gb" => 6,
            "G" => 7,
            "G#" => 8,"Ab" => 8,
            "A" => 9,
            "A#" => 10,"Bb" => 10,
            "B" => 11, "Cb" => 11,
            _ => throw new ArgumentException($"Unknown pitch name: {pitch}")
        };
        return (octave + 1) * 12 + pc;
    }

    public static int ExtractTrailingInt(string s, out int cutIndex)
    {
        cutIndex = s.Length;
        int i = s.Length - 1;
        while (i >= 0 && char.IsDigit(s[i]))
            i--;

        // iが数字以外の最後の位置なので、数字部分は i+1..end
        int start = i + 1;
        if (start >= s.Length)
            throw new ArgumentException($"No octave number in pitch: {s}");

        cutIndex = start;
        string num = s.Substring(start);
        if (!int.TryParse(num, out int val))
            throw new ArgumentException($"Invalid octave in pitch: {s}");

        return val;
    }
}
