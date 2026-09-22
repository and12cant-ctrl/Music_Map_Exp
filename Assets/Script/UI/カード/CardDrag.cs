using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private string eventCardId;
    public string EventCardId => string.IsNullOrEmpty(eventCardId) ? eventCardId = System.Guid.NewGuid().ToString("N") : eventCardId;
    public bool EventLocked { get; set; }
    public void BindEventCard(string id) { eventCardId = id; }
    public RectTransform DragLayer;
    //public string Name = "unko";
    private Image color;
    [SerializeField]public Image Iconimage;
    [SerializeField]public TextMeshProUGUI keyword;
    [SerializeField]public TextMeshProUGUI Num; 
    public CardType Type = CardType.Chara;
    public CardType TrueType = CardType.Chara;
    public MoneyTipe moneyType;
    [SerializeField]private int CardNum　= 0;
    public int _cardNum
    {
        get => CardNum;
        set
        {
            if (CardNum == value) return;
            CardNum = value;
            if (Num != null) Num.text = CardNum.ToString();
        }
    }

    private float _MarginError = 8f;

    private CanvasGroup canvasGroup;

    private RectTransform rect;
    public RectTransform parentSlotRect;

    private CardSlot cardSlot;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        color = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (Type == CardType.Money)
        {
            if (moneyType == MoneyTipe.Splite) Colorbind(4);
            else Colorbind(5);
        }
    }

    #region カードドラッグ処理
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (EventLocked) return;
        var previousSlot = transform.parent != null ? transform.parent.GetComponent<CardSlot>() : null;
        if (previousSlot != null && previousSlot.SlotInCard == this) previousSlot.SlotInCard = null;
        canvasGroup.blocksRaycasts = false;     //ドラッグ中RayCastを受けない
        transform.SetAsLastSibling();
        transform.SetParent(DragLayer, true);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!EventLocked) FollowMouse(eventData);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (EventLocked) return;
        canvasGroup.blocksRaycasts = true;

        // マウスを離した先にあるUIを取得
        var slot = eventData.pointerEnter?.GetComponentInParent<CardSlot>();
        if (slot == null)
        {
            ReturnCharaPareet();
            return;
        }
        slot.OnCardDropped(this);

    }
    #endregion

    public void SetDragArea(RectTransform area)
    {
        DragLayer = area;
    }

    public void ReturnCharaPareet()
    {
        var previousSlot = transform.parent != null ? transform.parent.GetComponent<CardSlot>() : null;
        if (previousSlot != null && previousSlot.SlotInCard == this) previousSlot.SlotInCard = null;
        if (parentSlotRect == null) { Debug.LogError("[CardDrag] Original card slot is missing."); return; }
        foreach (var other in parentSlotRect.GetComponentsInChildren<CardDrag>())
        {
            if (other != this && other.EventCardId == EventCardId && Type != CardType.Chara)
            {
                other._cardNum += _cardNum;
                _cardNum = 0;
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
        }
        if (rect == null) rect = GetComponent<RectTransform>();
        rect.SetParent(parentSlotRect, false);
        rect.localPosition = Vector3.zero;
    }

    public void CloneCard(int amount = 0)
    {
        if (amount <= 0 || amount >= _cardNum || parentSlotRect == null) return;
        _cardNum -= amount;
        GameObject clone = Instantiate(gameObject, parentSlotRect);
        clone.GetComponent<CardDrag>().BindEventCard(EventCardId);
        clone.transform.localPosition = Vector3.zero;
        _cardNum = amount;
    }
    private void Colorbind(int Def)
    {
        Color Excolors = Color.black;
        switch (Def)
        {
            case 0: 
                Excolors =  new Color32(173, 255, 47, 255); //緑
                break;
            case 1: Excolors = new Color32(0, 255, 255, 255);//青
                break;
            case 2: Excolors = new Color32(255, 238, 142, 255);//黄色
                break;
            case 3: Excolors= new Color32(255, 182, 193, 255);//ピンク
                break;
            case 4: Excolors = new Color32(24, 220, 150, 255);//青緑
                break;
            case 5: Excolors = new Color32(210 ,120,10,255);//茶色
                break;
        }
        color.color = Excolors;
    }

    private Vector2 ClampToArea(Vector2 targetPos, RectTransform area, RectTransform parentRect)
    {
        if (area == null) return targetPos;

        Vector3[] corners = new Vector3[4];
        area.GetWorldCorners(corners);

        Vector2 minLocal;
        Vector2 maxLocal;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[0]),
            null,
            out minLocal);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[2]),
            null,
            out maxLocal);

        float x = Mathf.Clamp(targetPos.x, minLocal.x, maxLocal.x);
        float y = Mathf.Clamp(targetPos.y, minLocal.y, maxLocal.y);

        return new Vector2(x, y);
    }

    private void FollowMouse(PointerEventData eventData)
    {
        rect.position = eventData.position;
    }
}

public enum CardType
{
    Chara,Skill,Equip,Money
}
public enum MoneyTipe
{
    Gold,Splite,None
}
