using System.Collections.Generic;
using UnityEngine;

public class CardCharaManeger : MonoBehaviour
{
    [SerializeField] private RectTransform ObjArea;
    [SerializeField] private RectTransform Ens;
    private List<CardDrag> cards = new List<CardDrag>();
    private List<CardSlot> slots = new List<CardSlot>();

    private void Awake()
    {
        slots.AddRange(GetComponentsInChildren<CardSlot>());
    }

    public void ChangeAllAreas()
    {
        foreach (var card in cards)
        {
            card.SetDragArea(ObjArea);
        }
    }

    public void Spawn(CardDrag prehubcard,CharacterRuntimeData chara)
    {
        int cnt = 1;
        if(chara.GetSkilltype() == CardType.Equip)
        {
            cnt--;
        }
        for (int num = 0; num <= cnt; num++)
        {
            CardDrag obj = Instantiate(prehubcard, slots[num].transform);
            switch (num)
            {
                case 0:
                    obj.Type = CardType.Chara;
                    break;
                case 1:
                    obj.Type = chara.GetSkilltype();
                    obj.keyword.text = chara.skillName();
                    obj.TrueType = CardType.Skill;
                    obj._cardNum = chara.skillcnt();

                    break;
                case 2:
                    obj.Type = CardType.Equip;
                    obj.TrueType = CardType.Equip;
                    break;
            }
            obj.BindEventCard(chara.runtimeId + "/" + num);
            obj.Iconimage.sprite = chara.Icon;
            obj.parentSlotRect = slots[num].transform as RectTransform;
            obj.DragLayer = this.transform as RectTransform;

            cards.Add(obj);
        }
    }
}
