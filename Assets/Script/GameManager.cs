using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    [Header("生成テンプレート")]
    public static GameManager Instance { get; private set; }
    [SerializeField] public  MapDataSO questMap;
    [SerializeField] private CharacterGenerator characterGenerator;
    [SerializeField] public MiniMapVisualConfig CellView;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform characterParent;
    [SerializeField] private int characterCount = 4;
    #region     テスト
    public BGCategoryListSO BGReqListSO { get; set; }
    public BGCategoryListSO BGRandListSO {  get; set; }
    public EventListSO EVReqLsitSO { get; set;}
    public EventListSO EVRandLsitSO {get; set;}
    public int MapGrid {  get; set; }
    #endregion
    private MapGenerator mapGenerator;
    private List<Character> _currentParty = new();
    private List<EventData> _eventDatas = new List<EventData>();
    private MiniMapBuilder miniMapBuilder;
    private Vector2 moveInput;
    public event Action<Vector2Int> onPlayerIconSet;
    private Vector2Int _playerCell = new Vector2Int(0, 0);

    [SerializeField] private TestManeger Test;

    public Vector2Int PlayerCell
    {
        get => _playerCell;
        set
        {
            if (_playerCell == value) return;
            _playerCell = value;
            onPlayerIconSet?.Invoke(_playerCell);
        }
    }

    void Start()
    {
        mapGenerator = GetComponent<MapGenerator>();
        miniMapBuilder = GetComponent<MiniMapBuilder>();

        Test.AtachManeger();
        questMap.EnsureGrid();
        #region nullTest
        Debug.Log($"questMap={questMap != null}, " +
          $"uiManager={uiManager != null}, " +
          $"mapGenerator={(mapGenerator != null)}" +
          $"questmap={questMap != null}" +
          $"BGReqListSO={BGReqListSO != null}" +
          $"BGRandListSO={BGRandListSO != null}" +
          $"EVReqLsitSO={EVReqLsitSO != null}" +
          $"EVRandLsitSO={EVRandLsitSO != null}");
        #endregion

        GenerateCharacters();           // キャラを生成
        uiManager.AssignCharacters(_currentParty);  // UIに割り当て
        mapGenerator.MapGenerate(questMap,BGReqListSO,BGRandListSO,EVReqLsitSO,EVRandLsitSO,MapGrid);
        onPlayerIconSet?.Invoke(PlayerCell);
        miniMapBuilder.SetupMap();
    }
    void Update()
    {
    }




    public void EndMission()
    {
        foreach (var c in _currentParty) Destroy(c.gameObject);

        _currentParty.Clear();

        Debug.Log("ミッション終了");
    }
    private void ClearPreviousMission()
    {
        if (_currentParty.Count > 0) EndMission();
    }
    private void GenerateCharacters()
    {
        _currentParty.Clear();
        characterGenerator.ResetNameCounts(); // 漢数字リセット（新ミッション開始）
        for (int i = 0; i < characterCount; i++)
        {
            CharacterRuntimeData runtime =
                characterGenerator.GenerateRandomCharacter();
            if (runtime == null) continue;

            // 1. プレハブを生成（ヒエラルキーに配置）
            GameObject go = Instantiate(characterPrefab, characterParent);

            // 2. Character スクリプトを取得
            Character chara = go.GetComponent<Character>();
            if (chara != null)
            {
                chara.runtime = runtime;
                _currentParty.Add(chara);
            }

            #region Nullチェック
            if (chara == null)
            {
                Debug.LogError("Character プレハブに Character.cs が付いていません！");
                continue;
            }
            #endregion
        }
    }

}
