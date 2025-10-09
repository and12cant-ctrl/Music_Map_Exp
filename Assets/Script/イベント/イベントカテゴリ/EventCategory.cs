using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/Event/category")]
[System.Serializable]
public class EventCategory : ScriptableObject
{
    public string evCategoryName;
    public Sprite catIcon;         // オプション：アイコン
    public List<EventData> evDatas= new List<EventData>();
}

[System.Serializable]
public class EventData
{
    public string id;           // 内部ID（例: "npc_villager_m"）
    public Sprite icon;         // オプション：アイコン
    public likeability likeLevel;
    public mystery mystery;
    public List<string> HakenPlayer;
}
public enum likeability
{
    Low,
    middle,
    High
}
public enum mystery
{
    Low,
    middle,
    High
}