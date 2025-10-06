using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/Facility List")]
public class FacilityListSO : ScriptableObject
{
    [System.Serializable]
    public class FacilityEntry
    {
        public string id;           // 内部ID（例: "facility_shrine"）
        public string displayName;  // 表示名（例: "古い神社"）
        public Sprite icon;         // オプション：マップ表示用アイコン
    }

    public List<FacilityEntry> facilityList = new List<FacilityEntry>();
}
