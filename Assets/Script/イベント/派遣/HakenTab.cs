using System.Collections.Generic;
using System.Drawing;
using TMPro;
using Unity.VisualScripting;
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
        Debug.Log($"buttonObj={buttonObj}");
        gm.OnPlayerIconSet += HakenTabView;
        gm.OnPlayerIconSet += HakenTabOut;
    }
    private void Update()
    {
        
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
            Debug.Log("Baka");
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
        for (int i = 0; i < gm._currentParty.Count; i++)
        {
            int index = i;
            Character entry = gm._currentParty[i];
            if (!entry.runtime.hakenCan) return;
            GameObject pm = Instantiate(Pair, Parent);
            pm.name = $"Pair_ {i}";
            var item = pm.GetComponent<PairItemBase>();
            var rt = pm.GetComponent<RectTransform>();

            if (item == null) Debug.Log("PairItemBaseがnull");
            item.pl_Name.text = entry.runtime.charaName;
            item.pl_API.text = $"{entry.runtime.curAPI}";
            item.hakenButton.onClick.AddListener(() =>
            {
                Debug.Log("unko");
                TogglePlayerState(index);
            });
            rt.anchoredPosition = new Vector2(5, (i * 23));
        }
        #endregion
    }
    void TogglePlayerState(int ind)
    {
        Debug.Log(gm._currentParty[ind].runtime.hakenCan);
        gm._currentParty[ind].runtime.hakenCan = false;
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
