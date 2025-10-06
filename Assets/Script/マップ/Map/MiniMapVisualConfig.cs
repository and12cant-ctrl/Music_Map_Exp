using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

[CreateAssetMenu(menuName = "Map/VisualConfig")]
public class MiniMapVisualConfig : ScriptableObject
{

    [FoldoutGroup("共通色")]
    [LabelText("全セル共通色")] public Color globalFillColor = new(0.1f, 0.3f, 0.6f, 0.5f);

    [FoldoutGroup("共通色")]
    [LabelText("塗りを透明化")] public bool makeFillTransparent = false;

    [FoldoutGroup("枠線")]
    [LabelText("枠線色")] public Color gridLineColor = Color.white;

    [FoldoutGroup("枠線")]
    [Range(1, 3), LabelText("枠線太さ(px)")] public int gridLineThickness = 1;

    [FoldoutGroup("エリア塗り"), TableList(AlwaysExpanded = true)]
    [LabelText("エリアリスト")] public List<AreaOverlay> areaOverlays = new();

    /// セル最終色（共通色＋エリア上塗り）
    public Color ResolveCellColor(int x, int y,string Aname)
    {
        var col = globalFillColor;
        if (makeFillTransparent) col.a = 0f;

        foreach (var area in areaOverlays)
        {
            if (!area.enabled) continue;
            if (area.name == Aname)
            {
                col = area.color;
                return area.color;
            }
        }
        return col;
    }
}
[System.Serializable]
public class AreaOverlay
{
    [LabelText("名前")] public string name = "Area";
    [LabelText("色")] public Color color = new(1f, 0f, 0f, 0.5f);
    [LabelText("有効")] public bool enabled = true;

}
