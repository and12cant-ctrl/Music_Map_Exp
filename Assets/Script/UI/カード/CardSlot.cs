using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CardSlot : MonoBehaviour
{
    [SerializeField] public CardType acceptType = CardType.Chara;

    [SerializeField] private bool onlyOneCard = false;
    private List<CardDrag> cards = new List<CardDrag>();
    public CardDrag SlotInCard;
    private Image BC;
    [SerializeField]private int SlotNum = 1;

    private void Start()
    {
        BC = GetComponentInChildren<Image>();
    }
    private void Update()
    {
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
        if (CanAccept(card))
        {
            Debug.Log("成功！");
            
                RectTransform cardRect = card.GetComponent<RectTransform>();
                RectTransform slotRect = GetComponent<RectTransform>();

                cardRect.SetParent(slotRect);
                cardRect.anchorMin = slotRect.anchorMin; cardRect.anchorMax = slotRect.anchorMax;
                cardRect.anchoredPosition = Vector2.zero;
                SlotInCard = card;
            if (SlotNum != 0 && card.TrueType != CardType.Chara)
            {
                card.CloneCard(SlotNum);
            }
        }
        else 
        {
            Debug.Log("失敗");
            card.ReturnCharaPareet(); 
        }
    }
    private bool CanAccept(CardDrag card)
    {
        bool rite = true;
        if (SlotInCard != null)
        {
            Debug.Log("カード指定無し");
            if (SlotInCard != card)
            {
                Debug.Log("指定カードでは無い");
                rite = false;
            }
        }
        else if (card.Type != acceptType)
        {
            Debug.Log("指定タイプではない");
            rite = false;
        }
        else if (SlotNum != 0)
        {
            if (card._cardNum < SlotNum & card.TrueType != CardType.Chara)
            {
                rite = false;
                Debug.Log(card._cardNum + SlotNum + "スキル回数が足りません");
            }
        }
        else if (onlyOneCard != false)
        {
            Debug.Log("失敗46");
            rite = false;
        }
        else if (transform.childCount > 1)
        {
            Debug.Log("既にカード入り");
            rite = false;
        }

        Debug.Log("成功");
        return rite;
    }
    public void ReturnCard(CardDrag card)
    {
        SlotInCard = null; 
        onlyOneCard = false;
        card.ReturnCharaPareet();
    }
}
