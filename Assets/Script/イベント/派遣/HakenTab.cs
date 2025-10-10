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
    private GameManager gm;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gm = gameObject.GetComponentInParent<GameManager>();
    }

    // Update is called once per frame
    void Haken()
    {
        Vector2Int PL = gm.PlayerCell;
        MapCell ia = gm.questMap.mapGrid[PL.x, PL.y];
        evName.text = ia.EvData.id;
        like_Lank.text = $"{ia.EvData.likeLevel}";
        mystery_Lank.text = $"{ia.EvData.mysteryLevel}";
        for (int i = 0; i < gm._currentParty.Count; i++)
        {
            pl_Name.text = $"{gm._currentParty[i].chara.name}";
            pl_API.text = $"{gm._currentParty[i].chara._api}";
            Pair.anchoredPosition=new Vector2 (5,74-(i*23));
        }
    }
}
