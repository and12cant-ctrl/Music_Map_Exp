using UnityEngine;
using UnityEngine.UI;

public class BackGroundDisplay : MonoBehaviour
{
    [SerializeField]private Image BackGround;
    private GameManager gm;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gm = gameObject.GetComponentInParent<GameManager>();
        gm.OnPlayerIconSet += BackGroundUpdate;
    }

    public void BackGroundUpdate(Vector2Int PL)
    {
        if (BackGround == null) Debug.Log("BackGroundÇ™Åvnull");
        string Time = "íã";
        MapCell ia = gm.questMap.mapGrid[PL.x, PL.y];
        BackGround.sprite = ia.bgCategory.GetSpriteById(Time);
    }
}
