using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapCell
{
    [ValueDropdown("GetEventIDs")]
    public string eventId;

    [ValueDropdown("GetBackgroundIDs")]
    public string backgroundId;
    public BackgroundCategory bgCategory;

    //public static EventListSO mapEVListSO;
    public EventData EvData;
    public static BGCategoryListSO mapBGListSO;

    public GameObject MapCellPre;

}
