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
    public static BackgroundListSO mapBGListSO;

    public GameObject MapCellPre;

    //private static IEnumerable<ValueDropdownItem<string>> GetEventIdDropdown()
    //{
    //    if (mapEVListSO = null)
    //    {
    //        yield return new ValueDropdownItem<string>("<None>", "");
    //       yield break; 
    //    };
    //    foreach(var ev in mapEVListSO.evCategoryList)
    //    {
    //        yield return new ValueDropdownItem<string>
    //            ($"{ev.evCategoryName}:{ev.name}", ev.evCategoryName);
    //    }
    //}  
    
    //private static IEnumerable<ValueDropdownItem<string>> GetBackgroundIdDropdown()
    //{
    //    if (mapBGListSO == null)
    //    {
    //        yield return new ValueDropdownItem<string>("<None>", "");
    //        yield break;
    //    };

    //    foreach (var category in mapBGListSO.bGCategoryList)
    //    {
    //        foreach(var entry in category.bgDatas)
    //        {
    //            string label = $"{category.bgCategoryName }/{ entry.displayName}";
    //            yield return new ValueDropdownItem<string>(label,entry.id);
    //        }
    //    }
    //}
}
