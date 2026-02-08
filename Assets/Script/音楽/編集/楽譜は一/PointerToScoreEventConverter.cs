using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using static Node_judge_re;


public static class PitchRange
{
    // G2..F5（あなたの固定音域）
    public const int MinMidi = 43; // G2
    public const int MaxMidi = 77; // F5

    // もし「中央C」なども使うならここに足す
    // public const int MiddleC = 60; // C4
}
public sealed class PointerToScoreEventConverter
{
    private readonly RectTransform _staffRect;
    private readonly Canvas _canvas;
    private readonly Camera _uiCamera;

    private readonly float _beatsPerBar;      // beat
    private readonly int _scaleSteps;         // scale
    private readonly float _nodeLength;       // nodeLength
    private readonly int _currentBar;         // currentBar
    private readonly float _pixelsPerBar;     // 330 など
    private readonly harfNode _halfNode;      // #/♭
    private readonly string[] _gcode;         // A..G

    public PointerToScoreEventConverter(
        RectTransform staffRect,
        Canvas canvas,
        Camera uiCamera,
        float beatsPerBar,
        int scaleSteps,
        float nodeLength,
        int currentBar,
        float pixelsPerBar,
        harfNode halfNode,
        string[] gcode)
    {
        _staffRect = staffRect;
        _canvas = canvas;
        _uiCamera = uiCamera;

        _beatsPerBar = beatsPerBar;
        _scaleSteps = Mathf.Max(1, scaleSteps);
        _nodeLength = nodeLength;
        _currentBar = Mathf.Max(1, currentBar);
        _pixelsPerBar = pixelsPerBar;
        _halfNode = halfNode;
        _gcode = gcode ?? new[] {"G","A", "B", "C", "D", "E", "F" };
    }

    public bool TryConvert(PointerEventData eventData, Part part, voice v, out ScoreEvent eScore)
    {
        eScore = default;

        // スナップテーブル
        double[] nodes = Enumerable.Range(0, (int)(2 * _beatsPerBar))
            .Select(i => i * _nodeLength * (1.0 / _beatsPerBar)).ToArray();

        int divY = Mathf.Max(2, _scaleSteps);
        double[] scales = Enumerable.Range(0, _scaleSteps)
            .Select(i => (double)i / _scaleSteps).ToArray();

        // Canvasの種類に応じたカメラ
        Camera cam = null;
        if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            cam = _uiCamera != null ? _uiCamera : _canvas.worldCamera;

        // 画面→ローカル
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_staffRect, eventData.position, cam, out Vector2 localPoint))
            return false;
        Vector2 size = _staffRect.rect.size;
        Vector2 half = size * 0.5f;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(localPoint.x, 0, size.x),
            Mathf.Clamp(localPoint.y, 0, size.y));

        // 正規化 0..1
        float nx = Mathf.InverseLerp(0, size.x, clamped.x);
        float ny = Mathf.InverseLerp(0, size.y, clamped.y);

        float snappedX = Normalize(nodes, nx);
        int midi = NormalizedYToMidi(ny, PitchRange.MinMidi, PitchRange.MaxMidi);
        string pitch = MidiToPitchString(midi, _halfNode);

        float snappedY = Normalize(scales, ny);
        if (snappedX >= 1f) return false;

        string halfSign =
            _halfNode == harfNode.Shape ? "#" :
            _halfNode == harfNode.Flat ? "♭" : "";

        float beatInBar = snappedX * _beatsPerBar;             
        float globalBeat = (_currentBar - 1) * _beatsPerBar + beatInBar;

        int bar = Mathf.FloorToInt(globalBeat / _beatsPerBar) + 1;
        float start = globalBeat - (bar - 1) * _beatsPerBar;   

        eScore = new ScoreEvent
        {
            part = part,
            voice = v,
            pitch = pitch,
            duration = _nodeLength,
            bar = bar,
            start = start
        };
        return true;
    }

    private static float Normalize(double[] separator, float ans)
    {
        if (separator == null || separator.Length == 0)
            throw new ArgumentException("separator が空です");

        float closest = (float)separator[0];
        float minDiff = Mathf.Abs(ans - closest);

        for (int i = 1; i < separator.Length; i++)
        {
            float value = (float)separator[i];
            float diff = Mathf.Abs(ans - value);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = value;
            }
        }
        return closest;
    }
    private static int NormalizedYToMidi(float snappedY, int minMidi, int maxMidi)
    {
        int lo = Mathf.Min(minMidi, maxMidi);
        int hi = Mathf.Max(minMidi, maxMidi);
        int range = hi - lo;

        // snappedY=0 → lo / snappedY=1 → hi
        int midi = lo + Mathf.RoundToInt(Mathf.Clamp01(snappedY) * range);
        return Mathf.Clamp(midi, lo, hi);
    }
    private static string MidiToPitchString(int midi, harfNode accidental)
    {
        string[] namesSharp =
        {
            "C","C#","D","D#","E","F","F#","G","G#","A","A#","B"
        };

        int note = ((midi % 12) + 12) % 12;
        int octave = (midi / 12) - 1;

        string name = namesSharp[note];

        if (accidental == harfNode.Flat)
        {
            // ♭表記に寄せる（内部用途なら "Db" 等が安全）
            name = name
                .Replace("C#", "Db")
                .Replace("D#", "Eb")
                .Replace("F#", "Gb")
                .Replace("G#", "Ab")
                .Replace("A#", "Bb");
        }
        else
        {
            // Shape(=Sharp) の場合はそのまま（C#,D#...）
            // #を付けたくない」等があるならここを分岐
        }

        return $"{name}{octave}";
    }
}
