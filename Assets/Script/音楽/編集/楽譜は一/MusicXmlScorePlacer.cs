using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MusicXML解析結果を ScoreEvent の列として受け取り、共通配置で譜面に反映する。
/// </summary>
public class MusicXmlScorePlacer : MonoBehaviour
{
    [SerializeField] private Node_Place nodeCheak;
    [SerializeField] private SimpleStaffRenderer staffRenderer;

    [Header("UI Layout")]
    [SerializeField] private float beatsPerBar = 4f;
    [SerializeField] private float pixelsPerBar = 330f;

    // pitch→Y座標の変換はプロジェクト仕様に依存するので、ここでは最小の関数で差し替え可能に
    public float PitchToNormalizedY(string pitch)
    {
        // TODO: "C4"等を scaleインデックスにマッピングして 0..1 を返す
        // 仮：中央固定
        return 0.5f;
    }

    public void PlaceFromScoreEvents(List<ScoreEvent> events, int baseBarForUI = 1)
    {
        var applier = new ScoreEventApplier(nodeCheak, staffRenderer);

        foreach (var e in events)
        {
            // UI座標を作る（クリックと同じルールに寄せる）
            float globalBeat = (e.bar - baseBarForUI) * beatsPerBar + e.start;
            float x = globalBeat * (pixelsPerBar / beatsPerBar);

            float ny = PitchToNormalizedY(e.pitch);
            float y = ny * staffRenderer.GetComponent<RectTransform>().rect.height; // 必要なら staffRect を参照するように修正

            var anchored = new Vector2(x, y);

            applier.ApplyPlace(e, anchored, "xml");
        }
    }
}
