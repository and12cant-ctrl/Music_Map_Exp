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

}
