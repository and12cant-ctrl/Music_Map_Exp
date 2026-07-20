using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Map/Event/ List")]
public class EventListSO : ScriptableObject
{
    public List<EventCategory> evCategoryList = new List<EventCategory>();

}
