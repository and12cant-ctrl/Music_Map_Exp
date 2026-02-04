using System;
using System.Collections.Generic;
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

    public ScoreEventApplier(Node_Place nodePlace, IStaffRenderer renderer)
    {
        _nodePlace = nodePlace;
        _renderer = renderer;
    }

    public bool ApplyPlace(ScoreEvent e, Vector2 anchoredPos, string nodeName)
    {
        if (!_nodePlace.PlaceNode(e.duration, e.part, e.pitch, e.start, e.bar))
            return false;

        _renderer?.RenderPlace(e, anchoredPos, nodeName);
        return true;
    }

    public void ApplyDelete(ScoreEvent e, Vector2 anchoredPos)
    {
        _nodePlace.DeleteNode(e.duration, e.part, e.pitch, e.start, e.bar);
        _renderer?.RenderDelete(e, anchoredPos);
    }
}
