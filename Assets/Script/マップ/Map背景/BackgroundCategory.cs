using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/BackgroundCategory")]
[System.Serializable]

public class BackgroundCategory : ScriptableObject
{
    public string bgCategoryName;
    public Color previewColor;   // 表示用の色（DrawCellで使う用）
    public List<BackgroundData> bgDatas = new List<BackgroundData>();
    public string GetRandomId()
    {
        if (bgDatas == null || bgDatas.Count == 0) return "default";
        return bgDatas[Random.Range(0, bgDatas.Count)].id;
    }
    public Sprite GetSpriteById(string id)
    {
        return bgDatas.Find(e => e.id == id)?.picture;
    }
}