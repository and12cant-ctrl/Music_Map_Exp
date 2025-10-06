using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public MapCoord CurrentCoord { get; private set; }

    public void SetPosition(MapCoord coord)
    {
        CurrentCoord = coord;

        //// もしマスに対応したワールド座標があるなら
        //Vector3 worldPos = new Vector3(coord.x, coord.y, 0);
        //transform.position = worldPos;
    }

    public void MoveTo(MapCoord newCoord, MapDataSO mapData)
    {
        //if (!mapData.IsValid(newCoord)) return;
        if (!CurrentCoord.GetNeighbors().Contains(newCoord)) return;

        SetPosition(newCoord);
        Debug.Log($"マス位置移動: {CurrentCoord}");
    }
}
