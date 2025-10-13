using TMPro;
using UnityEngine;

public class HakenTab : MonoBehaviour
{
    [SerializeField] private TMP_Text evName;
    [SerializeField] private TMP_Text like_Lank;
    [SerializeField] private TMP_Text mystery_Lank;
    [SerializeField] private TMP_Text pl_Name;
    [SerializeField] private TMP_Text pl_API;
    [SerializeField] private RectTransform Pair;
    [SerializeField] private GameObject Prefab;
    private GameManager gm;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gm = gameObject.GetComponentInParent<GameManager>();
        gm.onPlayerIconSet += HakenView;
    }

    // Update is called once per frame
    void HakenView(Vector2Int PL)
    {
        MapCell ia = gm.questMap.mapGrid[PL.x, PL.y];
        if (ia.EvData == null) return;
        Debug.Log(gm._currentParty.Count);
        for (int i = 0; i < gm._currentParty.Count; i++)
        {
            if (gm._currentParty[i].chara.exit == false) return;
            evName.text = ia.EvData.id;
            like_Lank.text = $"{ia.EvData.likeLevel}";
            mystery_Lank.text = $"{ia.EvData.mysteryLevel}";
            pl_Name.text = $"{gm._currentParty[i].chara.name}";
            pl_API.text = $"{gm._currentParty[i].chara._api}";
            Prefab.transform.localPosition=new Vector2 (5,74-(i*23));
        }
    }

    void HakenExe()
    {

    }
}
