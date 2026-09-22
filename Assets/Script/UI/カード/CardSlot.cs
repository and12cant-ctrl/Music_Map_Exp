using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardSlot : MonoBehaviour
{
    [SerializeField] public CardType acceptType = CardType.Chara;

    [SerializeField] private bool onlyOneCard = false;
    private List<CardDrag> cards = new List<CardDrag>();
    public CardDrag SlotInCard;
    private Image BC;
    private bool isEventSlot;
    [SerializeField]private int SlotNum = 1;

    public void ConfigureForEvent(CardType type, int amount)
    {
        isEventSlot = true;
        acceptType = type;
        SlotNum = amount;
        onlyOneCard = false;
        SlotInCard = null;
    }

    private void Start()
    {
        BC = GetComponentInChildren<Image>();
    }
    private void Update()
    {
        if (BC == null) return;
        if(onlyOneCard == true)
        {
            BC.color = Color.black;
        }
        else
        {
            BC.color = Color.blue;
        }
    }
    public void OnCardDropped(CardDrag card)
    {
        if (SlotInCard == card && card != null) return;
        if (CanAccept(card))
        {
            Debug.Log("ê¨å˜ÅI");
            
                RectTransform cardRect = card.GetComponent<RectTransform>();
                RectTransform slotRect = GetComponent<RectTransform>();

                cardRect.SetParent(slotRect);
                cardRect.anchorMin = slotRect.anchorMin; cardRect.anchorMax = slotRect.anchorMax;
                cardRect.anchoredPosition = Vector2.zero;
                if (isEventSlot)
                {
                    cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRect.anchoredPosition = new Vector2(0, 12);
                }
                SlotInCard = card;
            if (SlotNum != 0 && (isEventSlot ? card.Type != CardType.Chara : card.TrueType != CardType.Chara))
            {
                card.CloneCard(SlotNum);
            }
        }
        else 
        {
            Debug.Log("é∏îs");
            card.ReturnCharaPareet(); 
        }
    }
    public bool CanAccept(CardDrag card)
    {
        if (card == null || card.EventLocked || card.Type != acceptType) return false;
        if (SlotInCard != null && SlotInCard != card) return false;
        if (onlyOneCard && SlotInCard != card) return false;
        if (isEventSlot) return card.Type == CardType.Chara ? SlotNum == 1 : card._cardNum >= SlotNum;
        return SlotNum == 0 || card.TrueType == CardType.Chara || card._cardNum >= SlotNum;
    }
    public void ReturnCard(CardDrag card)
    {
        SlotInCard = null; 
        onlyOneCard = false;
        card.ReturnCharaPareet();
    }
}
