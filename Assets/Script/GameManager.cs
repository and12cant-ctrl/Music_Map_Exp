using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] public  MapDataSO questMap;
    [SerializeField] private CharacterGenerator characterGenerator;
    [SerializeField] public MiniMapVisualConfig CellView;
    [SerializeField] private CharaUIManager uiManager;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform characterParent;
    [SerializeField] public PartyManager PartyManager;
    #region     テスト
    public BGCategoryListSO BGReqListSO { get; set; }
    public BGCategoryListSO BGRandListSO {  get; set; }
    public EventListSO EVReqLsitSO { get; set;}
    public EventListSO EVRandLsitSO {get; set;}
    public int MapGrid {  get; set; }
    #endregion
    private MapGenerator mapGenerator;
    //[HideInInspector] public List<Character> _currentParty = new();
    private List<EventData> _eventDatas = new List<EventData>();
    [SerializeField] private TestManeger Test;
    private MiniMapBuilder miniMapBuilder;
    private Vector2 moveInput;
    public event Action<Vector2Int> OnPlayerIconSet;

    #region PlayerCell
    private Vector2Int _playerCell = new Vector2Int(5,5);
    public Vector2Int PlayerCell
    {
        get => _playerCell;
        set
        {
            int maxX = Mathf.Clamp(value.x, 0, MapGrid * 3);
            int maxY = Mathf.Clamp(value.y, 0, MapGrid * 3);

            Vector2Int clamped = new Vector2Int(maxX, maxY);

            if (_playerCell == clamped) return;
            _playerCell = clamped;
            OnPlayerIconSet?.Invoke(_playerCell);
        }
    }
    #endregion

    void Start()
    {
        mapGenerator = GetComponent<MapGenerator>();
        miniMapBuilder = GetComponent<MiniMapBuilder>();

        Test.AtachManeger();
        questMap.EnsureGrid();
        #region nullTest
        //Debug.Log($"questMap={questMap != null}, " +
        //  $"uiManager={uiManager != null}, " +
        //  $"mapGenerator={(mapGenerator != null)}" +
        //  $"questmap={questMap != null}" +
        //  $"BGReqListSO={BGReqListSO != null}" +
        //  $"BGRandListSO={BGRandListSO != null}" +
        //  $"EVReqLsitSO={EVReqLsitSO != null}" +
        //  $"EVRandLsitSO={EVRandLsitSO != null}");
        #endregion

        GenerateParty();           // キャラを生成
        uiManager.AssignCharacters(PartyManager.Runtime.Members);  // UIに割り当て
        mapGenerator.MapGenerate(questMap,BGReqListSO,BGRandListSO,EVReqLsitSO,EVRandLsitSO,MapGrid);
        OnPlayerIconSet?.Invoke(PlayerCell);
        miniMapBuilder.SetupMap();
    }
    void Update()
    {
    }
    public void EndMission()
    {
        //foreach (var c in PartyManager.Runtime.Members) Destroy(c.gameObject);

        PartyManager.Runtime.Members.Clear();

        Debug.Log("ミッション終了");
    }
    private void ClearPreviousMission()
    {
        if (PartyManager.Runtime.Members.Count > 0) EndMission();
    }
    private void GenerateParty()
    {
        PartyManager.Runtime.Members.Clear();
        characterGenerator.ResetNameCounts();   // 漢数字リセット（新ミッション開始）
        for (int i = 0; i < PartyManager.Runtime.PartyMemberNum; i++)
        {
            PartyManager.AddMember(characterGenerator.GenerateRandomCharacter());
        }
    }

}
