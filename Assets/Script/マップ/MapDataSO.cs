using Sirenix.OdinInspector;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using System.Linq;


[CreateAssetMenu(menuName = "Map/MapData")]
public class MapDataSO : SerializedScriptableObject
{
    [LabelText("マップ幅")]
    [MinValue(1)]
    public int width =0;

    [LabelText("マップ高さ")]
    [MinValue(1)]
    public int height =0;

    [ValueDropdown(nameof(GetBackgroundIdDropdown))]
    public string defaultBackgroundName;

    [TableMatrix(HorizontalTitle = "X →",SquareCells = true,DrawElementMethod = "DrawCell")]

    [NonSerialized, ShowInInspector]
    public MapCell[,] mapGrid = new MapCell[10, 10];

    [ValueDropdown("GetAllEventIDs")]
    [HideLabel, ShowInInspector]
    public string defaultEventId = "";
    [InlineEditor]
    public EventListSO eventListSO;

    [InlineEditor]
    public BGCategoryListSO backgroundListSO;
    [LabelText("選択開始座標")]
    public Vector2Int? selectionStart;

    [LabelText("選択終了座標")]
    public Vector2Int? selectionEnd;


    //private void OnEnable()
    //{
    //    // Static変数に代入（Odin Inspectorでも反映される）
    //    MapCell.mapEVListSO = eventListSO;
    //    MapCell.mapBGListSO = backgroundListSO;
    //}

    #region Inspectorからのマップ制作
    
    [Button("マップ初期化")]
    private void Init()
    {
        mapGrid = new MapCell[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mapGrid[x, y] = new MapCell();
            }
        }
        Debug.Log($"マップ {width}x{height} 初期化完了");
    }

    [Button("全セルを空にする")]
    private void Clear()
    {
        for (int y = 0; y < mapGrid.GetLength(1); y++)
        {
            for (int x = 0; x < mapGrid.GetLength(0); x++)
            {
                mapGrid[x, y] = new MapCell();
            }
        }
    }

    // ドロップダウン候補（後述の EventListSO から読み取る予定）
    private static IEnumerable<string> GetAllEventIDs()
    {
        return new[] { "npc001", "npc002", "eventA", "eventB" }; // 仮
    }

    // セル表示方法をカスタマイズ
    private static MapCell DrawCell(Rect rect, MapCell cell)
    {
        if (cell == null)cell = new MapCell();
        Color color = Color.gray; // デフォルトS
        if (MapCell.mapBGListSO != null && !string.IsNullOrEmpty(cell.backgroundId))
        {
            foreach (var category in MapCell.mapBGListSO.bGCategoryList)
            {
                if (category.bgCategoryName == cell.backgroundId)
                {
                    color = category.previewColor;
                    break;
                }

            }
        }

        // 背景描画
        EditorGUI.DrawRect(rect, color);

        #region 枠線の描画 
        Handles.BeginGUI();
        Handles.color = Color.black;
        Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y));           // 上
        Handles.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax));     // 右
        Handles.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax));     // 下
        Handles.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y));           // 左
        Handles.EndGUI();

        #endregion

        if (!string.IsNullOrEmpty(cell.eventId))
        {
            GUIStyle cellStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black },
                fontSize = 10
            };
            GUI.Label(rect, cell.eventId, cellStyle);
        }

        // ラベル（イベントID or 空）
        string label = string.IsNullOrEmpty(cell.eventId) ? "" : cell.eventId;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.black },
            fontSize = 10
        };

        GUI.Label(rect, label, style);

        // クリックでイベントIDトグル（デモ）
        //if (GUI.Button(rect, "", GUIStyle.none))
        //{
        //    cell.eventId = cell.eventId == "" ? MapCell.mapEVListSO?.evCategoryList.FirstOrDefault()?.evCategoryName ?? "" : "";
        //}

        return cell;
    }

    public void ApplyBackground(Vector2Int Start,Vector2Int End,BackgroundCategory bgCate)
    {
        if (!selectionStart.HasValue || !selectionEnd.HasValue )
            return;

        Vector2Int start = selectionStart.Value;
        Vector2Int end = selectionEnd.Value;
  
        for (int y = Mathf.Min(Start.y, End.y); y <= Mathf.Max(Start.y, End.y); y++)
        {
            for (int x = Mathf.Min(Start.x, End.x); x <= Mathf.Max(Start.x, End.x); x++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    mapGrid[x, y].bgCategory = bgCate;
                    Debug.Log(mapGrid[x, y].bgCategory);
                }
            }
        }
    }
    #endregion

    public void ApplyEvnet(int x,int y,EventData ev )
    {
        #region チェック
        // 1) 引数と内部状態の事前チェック
        if (ev == null)
        {
            Debug.LogError($"[ApplyEvnet] ev == null @({x},{y})");
            return;
        }
        if (mapGrid == null)
        {
            Debug.LogError("[ApplyEvnet] mapGrid == null");
            return;
        }
        // グリッド範囲チェック（幅・高さのプロパティ名はあなたの実装に合わせて）
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            Debug.LogError($"[ApplyEvnet] OutOfBounds x={x}, y={y}, w={width}, h={height}");
            return;
        }

        // 2) セルの null 安全化
        if (mapGrid[x, y] == null)
        {
            Debug.LogWarning($"[ApplyEvnet] mapGrid[{x},{y}] was null. Creating new MapCell.");
            mapGrid[x, y] = new MapCell();  // クラス/構造体に合わせて
        }
        #endregion

        mapGrid[x,y].EvData = ev;
    }

    [Button("選択範囲をログに出力")]
    #region ログ出力
    private void DebugLogSelectedRange()
    {
        if (!selectionStart.HasValue || !selectionEnd.HasValue)
        {
            Debug.LogWarning("選択範囲が未指定です");
            return;
        }

        Vector2Int start = selectionStart.Value;
        Vector2Int end = selectionEnd.Value;

        for (int y = Mathf.Min(start.y, end.y); y <= Mathf.Max(start.y, end.y); y++)
        {
            for (int x = Mathf.Min(start.x, end.x); x <= Mathf.Max(start.x, end.x); x++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    var cell = mapGrid[x, y];
                    if (cell != null)
                    {
                        Debug.Log($"Cell ({x},{y}): BG = {cell.backgroundId}, EV = {cell.eventId},Coler = ");
                    }
                    else
                    {
                        Debug.LogWarning($"Cell ({x},{y}) は null です");
                    }
                }
            }
        }
    }
    #endregion

    private IEnumerable<string> GetBackgroundIdDropdown()
    {
        if (backgroundListSO == null)return new[] { "<None>" };

        var list = new List<string>();
        foreach (var category in backgroundListSO.bGCategoryList)
        {
            var entry = category.bgCategoryName;
                list.Add(entry);
        }
        return list;
    }
    public Color BGColor(MapCell cell)
    {
        var Bg = cell.bgCategory.previewColor;
        return Bg;
    }

    public void EnsureGrid()
    {
        if(mapGrid == null || mapGrid.GetLength(0) != width || mapGrid.GetLength(1) != height)
        mapGrid = new MapCell[width, height];

        for(int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                if(mapGrid[x, y] == null )
                mapGrid[x, y] = new MapCell();
            }
        }
    }
}
