using UnityEngine;
using UnityEngine.UI;
public class MapIconPlacer : MonoBehaviour
{
    [SerializeField]public RectTransform iconLayer;
    [SerializeField]public MiniMapBuilder mapBuilder;

    public GameObject iconPrefab;

    public RectTransform PlaceIcon(int mx, int my, Sprite sprite)
    {
        var go = Instantiate(iconPrefab, iconLayer);
        //var img = go.GetComponent<Image>
        //img.sprite = sprite;

        var rt = go.GetComponent<RectTransform>();

        // マップ座標→UI座標変換
        float x = mx * mapBuilder.cellPx;
        float y = -my * mapBuilder.cellPx; // 上下反転に注意（uvRectが下原点のため）
        rt.anchoredPosition = new Vector2(x, y);

        return rt;
    }

}
