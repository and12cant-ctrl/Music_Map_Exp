using UnityEngine;

public class TestManeger : MonoBehaviour
{

    [SerializeField] GameManager gameManager;
    [SerializeField] private BGCategoryListSO BgReqListSO;
    [SerializeField] private BGCategoryListSO BgRandListSO;
    [SerializeField] private EventListSO EvReqLsitSO;
    [SerializeField] private EventListSO EvRandLsitSO;
    [SerializeField] private int MapGrid;
    [ContextMenu("クエスト画面テスト")]
    public void AtachManeger()
    {
        gameManager.BGReqListSO = BgReqListSO;
        gameManager.BGRandListSO = BgRandListSO;
        gameManager.EVReqLsitSO = EvReqLsitSO;
        gameManager.EVRandLsitSO = EvRandLsitSO;
        gameManager.MapGrid = MapGrid;
        print($"{gameManager.BGReqListSO}, " +
            $"{gameManager.BGRandListSO}," +
            $"{gameManager.EVReqLsitSO}," +
            $"{gameManager.EVRandLsitSO}," +
            $"{gameManager.MapGrid}");
    }
    private void Start()
    {
        
    }
}
