using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[DefaultExecutionOrder(0)]
public class MiniMapBuilder : MonoBehaviour
{
    [Header("Full Map")]
    [SerializeField] private GameObject fullMapPanel; // 開閉のみ
    [SerializeField] private RectTransform fullGridParent;
    [SerializeField] public RawImage fullmapImage;
    [SerializeField] public RectTransform fMapIconRect;

    [Header("Mini Map (9x9)")]
    [SerializeField] private RectTransform miniGridParent;
    [SerializeField] public RawImage miniMapImage;
    [SerializeField] private Sprite PlayerIcon;
    [SerializeField] public RectTransform mMapIconRect;
    private MapIconPresenter iconPlacer;
    public RectTransform maskWindow; // RectMask2D 付き

    private GameManager gm;
    private MapDataSO map;

    public int cellPx { get; private set; } = 18; // セルサイズ
    public int viewSize { get; private set; } = 7;
    public int borderThickness { get; private set; } = 2;
    public int texH { get; private set; }
    public int texW { get; private set; }

    private int winCell;

    private Color borderColor = Color.white;
    private Texture2D _tex;
    private bool _built;
    public bool IsBuilt => _built;
    public string pLString = "PLchara";
    public void SetupMap()
    {
        gm = GetComponent<GameManager>();
        iconPlacer = GetComponent<MapIconPresenter>();
        map = gm.questMap;

        BuildFullMapOnce();
        SetupMiniMapBorder();
        ShowWindow(gm.PlayerCell.x, gm.PlayerCell.y);
        Debug.Log($"PLのセル{gm.PlayerCell.x}{gm.PlayerCell.y}");
        ShowFullMap(false);
        gm.OnPlayerIconSet -= PlayerIconSet;
        gm.OnPlayerIconSet += PlayerIconSet;
    }

    #region map生成
    public void ResetMapView()
    {
        if (gm != null) gm.OnPlayerIconSet -= PlayerIconSet;
        GetComponent<MapIconPresenter>()?.ClearIcons();
        if (_tex != null) Destroy(_tex);
        _tex = null;
        _built = false;
    }

    private void BuildFullMapOnce()
    {
        if (_built) return;
        fullmapImage = fullGridParent.GetComponent<RawImage>();
        miniMapImage = miniGridParent.GetComponent<RawImage>();

        int mapH = map.height, mapW = map.width;
        texH = mapH * cellPx ;
        texW = mapW * cellPx;     //ピクセル解像度

        winCell =viewSize * cellPx;

        _tex = new Texture2D( texW , texH, TextureFormat.RGBA32, mipChain: false);
        _tex.filterMode = FilterMode.Point;      // 拡大縮小時ににじませない      
        _tex.wrapMode = TextureWrapMode.Clamp; // UVがはみ出した時に端を繰り返さない

        var pixels = new Color32[texW * texH];      //バッファ

        for (int my = 0; my < mapH; my++)
        {
            for (int mx = 0; mx < mapW; mx++)
            {
                Color32 c = gm.CellView.ResolveCellColor(mx, my, CellInt(mx, my));

                int startPosX = mx * cellPx;
                int startPosY = my * cellPx;
                var tage = map.mapGrid[mx, my];

                var system = gm.GetComponent<MusicMap.Events.EventSystemRoot>();
                if (!string.IsNullOrEmpty(tage.eventInstanceId) && system?.Manager != null &&
                    system.Manager.TryGetDefinition(tage.eventInstanceId, out var instance, out var definition, out _))
                {
                    if (definition.appearance.mapIcon != null)
                        iconPlacer.CreateIcon(instance.instanceId, definition.appearance.mapIcon, mx, my);
                }
                else if (tage.EvData != null && tage.EvData.icon != null)
                {
                    iconPlacer.CreateIcon(tage.EvData.id, tage.EvData.icon,mx,my);
                }
                for (int py = 0; py < cellPx; py++)
                {
                    int yy = startPosY + py;
                    int row = yy * texW;
                    int dst = row + startPosX;

                    for (int px = 0; px < cellPx; px++) pixels[dst + px] = c;
                }
            }
        }
        iconPlacer.CreateIcon(pLString, PlayerIcon, gm.PlayerCell.x, gm.PlayerCell.y);

        DrawGridLines(pixels, texW, texH, Color.white);
        _tex.SetPixels32(pixels);
        _tex.Apply(false, false);

        fullmapImage.texture = _tex;
        miniMapImage.texture = _tex;

        #region マップ表示
        fullmapImage.uvRect = new Rect(0, 0, 1, 1); // 全域を表示（切り抜きなし）
        fullGridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, texW);
        fullGridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, texH);

        fMapIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, texW);
        fMapIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, texH);

        #region     ミニマップサイズ調整
        miniGridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, winCell);
        miniGridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, winCell);
        mMapIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, winCell);
        mMapIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, winCell);
        #endregion
        #endregion

        EnsureBorder(fullGridParent,borderColor,borderThickness,texW);

        _built = true;
    }
    void SetupMiniMapBorder()
    {
        // 窓サイズは viewSize*cellPx （整数で）
        winCell = viewSize * cellPx+borderThickness;
        maskWindow.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, winCell);
        maskWindow.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, winCell);

        // 外枠（UI）。途切れ防止のためテクスチャでなくUIで重ねる
        EnsureBorder(miniGridParent, borderColor,  borderThickness, winCell);
    }
    void EnsureBorder(RectTransform mw, Color color, int thicknessPx,int tex)
    {
        CreateEdge("Border_Top",    new Vector2(1f, 1f),new Vector2(1f, 1f),new Vector2(0, thicknessPx), new Vector2(tex ,thicknessPx));
        CreateEdge("Border_Bottom", new Vector2(0f, 0f),new Vector2(0f, 0f),new Vector2(0,0),new Vector2(tex + thicknessPx,thicknessPx));
        CreateEdge("Border_Left",  new Vector2(0f, 0f),new Vector2(0f, 0f),new Vector2(0, 0),new Vector2(thicknessPx, tex +thicknessPx));
        CreateEdge("Border_Right", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(thicknessPx,thicknessPx), new Vector2(thicknessPx, tex + (thicknessPx * 2)));

        void CreateEdge(string name, Vector2 pivot, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            if (mw.Find(name) != null) return; // 二重生成防止
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mw, false);
            var rt = (RectTransform)go.transform;
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }
    public void ShowWindow(int viewLeft, int viewTop)
    {
        if (!_built) BuildFullMapOnce();
        int mapH = map.height, mapW = map.width;
        float sizeHarf = viewSize / 2;

        float cX = Mathf.Clamp(viewLeft, sizeHarf, Mathf.Max(0, mapW - sizeHarf));
        float cY = Mathf.Clamp(viewTop, sizeHarf , Mathf.Max(0, mapH - sizeHarf));
        //範囲外を原点にしないようにする処理

        // RawImage.uvRect は「下原点」。上原点→下原点へ変換が必要
        float hLeft = (cX-sizeHarf) / (float)mapW;
        float vBottom = (cY-sizeHarf) / (float)mapH; // 上原点

        float vh = viewSize / (float)mapH;
        float uw = viewSize / (float)mapW;

        //Debug.Log($"{hLeft}, {vBottom}, {uw}, {vh}");

        miniMapImage.uvRect = new Rect(hLeft, vBottom, uw, vh);

        mMapIconRect.anchoredPosition = new Vector2
            (((cX-sizeHarf)*-cellPx) -((float)1.5),
            (cY-sizeHarf)*-cellPx- ((float)1.5));
    }
    public void BatchUpdateCells((int mx, int my, Color32 c)[] changes)
    {
        if (_tex == null || changes == null || changes.Length == 0) return;
        int mapH = map.height, mapW = map.width;

        foreach (var ch in changes)
        {
            if ((uint)ch.mx >= mapW || (uint)ch.my >= mapH) continue;

            int texX = ch.mx * cellPx;
            int texY = ch.my * cellPx;

            var block = new Color32[cellPx * cellPx];
            for (int i = 0; i < block.Length; i++) block[i] = ch.c;

            _tex.SetPixels32(texX, texY, cellPx, cellPx, block);
        }
        _tex.Apply(false, false);
    }
    public void PlayerIconSet(Vector2Int cell)
    {
        ShowWindow(cell.x, cell.y);
        iconPlacer.UpdateIcon(pLString, cell.x,cell.y);
    }
    #endregion
    public void ShowFullMap(bool show) => fullMapPanel.SetActive(show);

    // fullMapの境界線を描く
    private void DrawGridLines(Color32[] pixels, int texW, int texH, Color lineCol)
    { 
        int maxX = texW+borderThickness;
        int maxY = texH+borderThickness;
        // 横線：セルの高さごとに1本
        for (int k = 0; k <= map.height+1; k++)
        {
            int y0 = (k < map.height) ? k * cellPx : maxX;    //Y軸セルの個数
            for (int t = 0; t < borderThickness; t++)
            {
                int y = y0 + t; 
                if (y >= texH)  break;
                int row = y * texW;
                for (int x = 0; x < texW; x++)  pixels[row + x] = lineCol;
            }
        }
        // 縦線：セルの幅ごとに1本
        for (int k = 0; k <= map.width+1; k++)
        {
            int x0 = (k < map.width) ? k * cellPx : maxY;
            for (int t = 0; t < borderThickness; t++)
            {
                int x = x0 + t; if (x >= texW)  break;
                for (int y = 0; y < texH; y++)  pixels[y * texW + x] = lineCol;
            }
        }
    }

    string CellInt(int ix,int iy)
    {
        var eventSystem = gm.GetComponent<MusicMap.Events.EventSystemRoot>();
        var selectedCell = map.mapGrid[ix, iy];
        if (!string.IsNullOrEmpty(selectedCell?.eventInstanceId) && eventSystem?.Manager != null &&
            eventSystem.Manager.TryGetDefinition(selectedCell.eventInstanceId, out _, out var definition, out _) &&
            !string.IsNullOrEmpty(definition.appearance.mapColorKey)) return definition.appearance.mapColorKey;
        string von = "";
        if (map.mapGrid[ix, iy] == null){
            von = "エリア外";
        }else if(map.mapGrid[ix, iy].EvData == null){
            von = "それ以外";
        }else{
            switch (map.mapGrid[ix, iy].EvData.id)
            {
                case string s when s.Contains("妖怪"):
                    von = "敵";
                    break;
                case string s when s.Contains("神社"):
                    von = "施設";
                    break;
                default:
                    von = "それ以外";
                    break;
            }
        }
    return von;   
    }

}


