using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapIconPresenter : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform iconLayerFull;   // FullMapRoot の IconLayer
    public RectTransform iconLayerMini;   // MiniMapRoot の IconLayer

    [SerializeField] private MiniMapBuilder builder;
    [Header("Prefab / default size")]
    [SerializeField] private GameObject iconPrefab;

    //public RawImage miniMapImage;    // ミニの RawImage（uvRectをShowWindowAtで更新）
    private int mapW, mapH;
    private int cellPx => builder.cellPx;
    private int viewSize => builder.viewSize;             // ミニの窓（10x10）

    private Vector2 IconSize;

    float thisCellPx => builder.cellPx * 0.7f;
    Vector2 CellToUI(Vector2Int cell)
    {
        return new Vector2((cell.x * cellPx) + builder.borderThickness * 2 ,
            ((-cell.y-1) * cellPx) + builder.borderThickness * 2);
    }
    public class IconPair
    {
        public RectTransform mini;
        public RectTransform full;
    }
    void Awake()
    {
        IconSize = new Vector2(thisCellPx, thisCellPx);
    }

    public Dictionary<string, IconPair> _icons = new();

    public void CreateIcon(string id, Sprite sprite, int x, int y)
    {
        Vector2Int cell = new Vector2Int(x, -y-1);
        if (_icons.ContainsKey(id)) { UpdateIcon(id,x,y); return; }
        #region Full
        var full = Instantiate(iconPrefab, iconLayerFull).GetComponent<RectTransform>();
        var imgF = full.GetComponent<Image>();
        full.gameObject.name = id;
        imgF.sprite = sprite;
        full.sizeDelta = IconSize;
        full.anchoredPosition = CellToUI(cell);
        #endregion
        #region Mini
        var mini = Instantiate(iconPrefab, iconLayerMini).GetComponent<RectTransform>();
        var imgM = mini.GetComponent<Image>();
        mini.gameObject.name = id;
        imgM.sprite = sprite;
        mini.sizeDelta = IconSize;
        mini.anchoredPosition = CellToUI(cell);

        #endregion
        _icons[id] = new IconPair { mini = mini, full = full };
    }

    public void UpdateIcon(string id, int x,int y)
    {
        if (!_icons.TryGetValue(id, out var put)) return;
        Vector2Int cell = new Vector2Int(x, -y - 1);
        var pos = CellToUI(cell);
        if (put.full) put.full.anchoredPosition = pos;
        if (put.mini) put.mini.anchoredPosition = pos;
    }

    public void RemoveIcon(string id)
    {
        if (!_icons.TryGetValue(id, out var put)) return;
        if (put.full) Destroy(put.full.gameObject);
        if (put.mini) Destroy(put.mini.gameObject);
        _icons.Remove(id);
    }
    public void ClearIcons()
    {
        foreach (var pair in _icons.Values)
        {
            if (pair.full) { pair.full.gameObject.SetActive(false); Destroy(pair.full.gameObject); }
            if (pair.mini) { pair.mini.gameObject.SetActive(false); Destroy(pair.mini.gameObject); }
        }
        _icons.Clear();
    }

    public void SyncMiniWindow(int viewLeft, int viewTop)
    {
        // IconLayer を RawImage の uvRect 移動と逆向きにシフト
        iconLayerMini.sizeDelta = new Vector2(viewSize * cellPx, viewSize * cellPx);
        iconLayerMini.anchoredPosition = new Vector2(-viewLeft * cellPx, +viewTop * cellPx);
    }

    // 表示のON/OFF（SetActiveは外でやってOKだが、アイコン側の可視切替も用意しておくと便利）
    public void SetMiniVisible(bool v) { if (iconLayerMini) iconLayerMini.gameObject.SetActive(v); }
    public void SetFullVisible(bool v) { if (iconLayerFull) iconLayerFull.gameObject.SetActive(v); }

    // ===== ユーティリティ =====
    
}