using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using static Node_judge;

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
        _gcode = gcode ?? new[] { "A", "B", "C", "D", "E", "F", "G" };
    }

    public bool TryConvert(PointerEventData eventData, Part part, voice v, out ScoreEvent e, out Vector2 anchoredPos)
    {
        e = default;
        anchoredPos = default;

        // スナップテーブル
        double[] nodes = Enumerable.Range(0, (int)(2 * _beatsPerBar))
            .Select(i => i * _nodeLength * (1.0 / _beatsPerBar)).ToArray();

        double[] scales = Enumerable.Range(0, _scaleSteps)
            .Select(i => (double)i / _scaleSteps).ToArray();

        // Canvasの種類に応じたカメラ
        Camera cam = null;
        if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            cam = _uiCamera != null ? _uiCamera : _canvas.worldCamera;

        // 画面→ローカル
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_staffRect, eventData.position, cam, out Vector2 localPoint))
            return false;

        Vector2 half = _staffRect.rect.size * 0.5f;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(localPoint.x, -half.x, half.x),
            Mathf.Clamp(localPoint.y, -half.y, half.y));

        // 正規化 0..1
        float nx = Mathf.InverseLerp(-half.x, half.x, clamped.x);
        float ny = Mathf.InverseLerp(-half.y, half.y, clamped.y);

        float snappedX = Normalize(nodes, nx);
        float snappedY = Normalize(scales, ny);
        if (snappedX >= 1f) return false;

        // ---- ここから「譜面情報」へ ----

        // pitch生成
        int beatY = Mathf.RoundToInt(snappedY * 100f);

        string halfSign =
            _halfNode == harfNode.Shape ? "#" :
            _halfNode == harfNode.Flat ? "♭" : "";

        string pitch = $"{_gcode[(beatY / 5) % _gcode.Length]}{halfSign}{(((beatY / 5) + 6) / _gcode.Length) + 3}";

        // snappedX は「この小節(表示領域)の0..1」なので、拍位置へ
        float beatInBar = snappedX * _beatsPerBar;             // 0..beatsPerBar
        float globalBeat = (_currentBar - 1) * _beatsPerBar + beatInBar;

        int bar = Mathf.FloorToInt(globalBeat / _beatsPerBar) + 1;
        float start = globalBeat - (bar - 1) * _beatsPerBar;   // 0..beatsPerBar

        // UI座標
        float ansX = Mathf.Lerp(0, _staffRect.rect.width , snappedX); //割合で×して返す
        float ansY = Mathf.Lerp(0, _staffRect.rect.height, snappedY);

        anchoredPos = new Vector2(ansX + ((_currentBar - 1) * _pixelsPerBar), ansY);

        e = new ScoreEvent
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
}
