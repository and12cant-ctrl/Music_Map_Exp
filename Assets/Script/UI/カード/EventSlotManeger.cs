using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EventSlotManeger : MonoBehaviour
{
    [SerializeField] private Button ResetBut;
    private bool TpdayEve = false;
    private bool TpdayEveComplte = false;
    List<CardSlot> Sloters = new List<CardSlot>();

    void Start()
    {
        Sloters = new List<CardSlot>(GetComponentsInChildren<CardSlot>());
        ResetBut.onClick.AddListener(() =>
        {
            foreach (CardSlot slot in Sloters)
            {
                if (slot.SlotInCard != null)
                {
                    Debug.Log($"{Sloters.Count}_スロットカウント");
                    slot.ReturnCard(slot.SlotInCard);
                }

            }
        });
    }
}
