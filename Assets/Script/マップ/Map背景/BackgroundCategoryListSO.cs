
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Map/Background List")]
public class BackgroundListSO : ScriptableObject
{
    public List<BackgroundCategory> bGCategoryList = new List<BackgroundCategory>();
    public BackgroundData GetDataById(string id)
    {
        foreach (var category in bGCategoryList)
        {
            var data = category.bgDatas.Find(e => e.id == id);
            if (data != null)
                return data;
        }
        return null;
    }
}
