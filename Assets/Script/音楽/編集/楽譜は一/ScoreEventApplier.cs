using System;
using UnityEngine;

[Serializable]
public struct ScoreEvent
{
    public Part part;
    public voice voice;
    public string pitch;     // 例: "C#4" / "D♭4" / "REST"
    public float duration;   // nodeLength と同じ単位（四分=1.0なら八分=0.5）
    public int bar;          // 1-based
    public float start;      // 小節内の拍位置（0.0 start）
}

public interface IStaffRenderer
{
    void RenderPlace(ScoreEvent e, Vector2 anchoredPos, string nodeName);
    void RenderDelete(ScoreEvent e, Vector2 anchoredPos);
}

/// <summary>
/// クリックでもMusicXMLでも共通で「譜面イベント」をNode_Placeに反映し、必要ならUIも更新する。
/// </summary>
public sealed class ScoreEventApplier
{
    private readonly Node_Place _nodePlace;
    private readonly IStaffRenderer _renderer;
    private readonly StaffCoordinateConverter _coord;
    public ScoreEventApplier(Node_Place nodePlace, IStaffRenderer renderer,StaffCoordinateConverter coord)
    {
        _nodePlace = nodePlace;
        _renderer = renderer;
        _coord = coord ?? throw new ArgumentNullException(nameof(coord));
    }

    public bool ApplyPlace(ScoreEvent e, string nodeName)
    {
        float snappedStart = SnapBeat(e.start, 4f); // 16分にスナップ

        if (!_nodePlace.PlaceNode(e.duration, e.part, e.pitch, snappedStart, e.bar))
            return false;

        var snappedEvent = e;
        snappedEvent.start = snappedStart;

        Vector2 anchoredPos = _coord.ToAnchoredPosition(snappedEvent);
        _renderer?.RenderPlace(snappedEvent, anchoredPos, nodeName);
        return true;
    }
    public void ApplyDelete(ScoreEvent e)
    {
        _nodePlace.DeleteNode(e.duration, e.part, e.pitch, e.start, e.bar);
        Vector2 anchoredPos = _coord.ToAnchoredPosition(e);
        _renderer?.RenderDelete(e, anchoredPos);
    }
    private float SnapBeat(float beat, float gridPerQuarter = 4f)
    {
        // gridPerQuarter=4 なら 16分単位（1/4拍）
        float step = 1f / gridPerQuarter;
        return Mathf.Round(beat / step) * step;
    }


}
