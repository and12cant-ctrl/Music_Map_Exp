using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HakenTab : MonoBehaviour
{
    [SerializeField] private TMP_Text evName;
    [SerializeField] private TMP_Text like_Lank;
    [SerializeField] private TMP_Text mystery_Lank;
    [SerializeField] private GameObject Pair;
    [SerializeField] private RectTransform Parent;
    [SerializeField] private GameObject Tab;
    [SerializeField] private GameObject boord; 
    private GameManager gm;
    private Button buttonObj;
    private bool tabIn = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gm = gameObject.GetComponentInParent<GameManager>();
        buttonObj = Tab.GetComponent<Button>();
        gm.OnPlayerIconSet += HakenTabView;
        gm.OnPlayerIconSet += HakenTabOut;
    }

    void HakenTabView(Vector2Int PL)
    {
        MapCell PL2Int = gm.questMap.mapGrid[PL.x, PL.y];
        if (PL2Int == null||PL2Int.EvData == null) return;
        Tab.SetActive(true);
        tabIn=(true);
        buttonObj.onClick.RemoveAllListeners();
        buttonObj.onClick.AddListener(() =>
        {
            boord.SetActive(true);
            HakenBoard(PL2Int);
        });  
    }
    void HakenBoard(MapCell pl_Int)
    {
        evName.text = pl_Int.EvData.id;
        like_Lank.text = $"{pl_Int.EvData.likeLevel}";
        mystery_Lank.text = $"{pl_Int.EvData.mysteryLevel}";

        for (int i = Parent.childCount - 1; i >= 0; i--) Destroy(Parent.GetChild(i).gameObject);

        #region キャラ生成
        for (int i = 0; i < gm.PartyManager.Runtime.Members.Count; i++)
        {
            int index = i;

            if (!gm.PartyManager.Runtime.Members[i].hakenCan) return;
            GameObject pm = Instantiate(Pair, Parent);
            pm.name = $"Pair_ {i}";
            var item = pm.GetComponent<PairItemBase>();
            var rt = pm.GetComponent<RectTransform>();

            if (item == null) Debug.Log("PairItemBaseがnull");
            item.pl_Name.text = gm.PartyManager.Runtime.Members[i].charaName;
            item.pl_API.text = $"{gm.PartyManager.Runtime.Members[i].curApi}";
            item.hakenButton.onClick.AddListener(() =>
            {
                TogglePlayerState(index);
            });
            rt.anchoredPosition = new Vector2(5, (i * 23));
        }
        #endregion
    }
    void TogglePlayerState(int ind)
    {
        gm.PartyManager.Runtime.Members[ind].hakenCan = false;
        //ここに派遣のイベント処理を入れる
        boord.SetActive(false);
        Tab.SetActive(false);
    }

    void HakenTabOut(Vector2Int PL)
    {
        MapCell PL2Int = gm.questMap.mapGrid[PL.x, PL.y];
        if (tabIn == true && PL2Int.EvData == null) 
        {
            Tab.SetActive(false);
            boord.SetActive(false);
        }
    }

}
