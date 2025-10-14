using TMPro;
using UnityEngine;

public class HakenTab : MonoBehaviour
{
    [SerializeField] private TMP_Text evName;
    [SerializeField] private TMP_Text like_Lank;
    [SerializeField] private TMP_Text mystery_Lank;
    [SerializeField] private GameObject Pair;
    [SerializeField] private RectTransform Parent;
    private GameManager gm;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gm = gameObject.GetComponentInParent<GameManager>();
        gm.onPlayerIconSet += HakenExe;
    }


    void HakenExe(Vector2Int PL)
    {
        MapCell PL2Int = gm.questMap.mapGrid[PL.x, PL.y];
        if (PL2Int == null||PL2Int.EvData == null) return;
        evName.text = PL2Int.EvData.id;
        like_Lank.text = $"{PL2Int.EvData.likeLevel}";
        mystery_Lank.text = $"{PL2Int.EvData.mysteryLevel}";
        for (int i = Parent.childCount - 1; i >= 0; i--)Destroy(Parent.GetChild(i).gameObject);
        
        for (int i = 0; i < gm._currentParty.Count; i++)
        {
            var entry = gm._currentParty[i];
            GameObject pm = Instantiate(Pair,Parent);
            pm.name = $"Pair_ {i}";


            var item = pm.GetComponent<PairItemBase>();
            if (item != null)
            {
                Debug.Log(entry.runtime.charaName);
                item.pl_Name.text = entry.runtime.charaName;
                item.pl_API.text = $"{entry.runtime.curAPI}";
            }
            var rt = pm.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(5,  (i * 23));
            }
        }
    }
}
