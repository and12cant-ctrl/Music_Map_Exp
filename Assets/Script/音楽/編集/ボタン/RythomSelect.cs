using UnityEngine;
using UnityEngine.UI;
using static Node_judge;

public class RythomSelect : MonoBehaviour
{
    [SerializeField] private Node_judge Edi_Score;
    [SerializeField] private TabOverLayCloser TabClose;
    [SerializeField] private Button OpenTab;
    [SerializeField] private GameObject RhyhomTab;
    [SerializeField] private Button btnHarf;
    [SerializeField] private Button btnOne;
    [SerializeField] private Button btnTwo;
    [SerializeField] private Button btnAll;
    [SerializeField] private Sprite[] Nodesprites;
    private float LenNo = 0.5f;
    void Start()
    {
        OpenTab.onClick.AddListener(() =>
        {
            TabClose.OpenTab(RhyhomTab);
        });
        #region 音符改変
        btnHarf.onClick.AddListener(() =>
        {
            NodeLen(LengthNode.Harf);
        });
        btnOne.onClick.AddListener(() =>
        {
            NodeLen(LengthNode.One);
        });
        btnTwo.onClick.AddListener(() =>
        {
            NodeLen(LengthNode.Two);

        });
        btnAll.onClick.AddListener(() =>
        {
            NodeLen(LengthNode.all);
        });
        #endregion
        ChangeImage(Edi_Score.NodePrehab, Nodesprites[0]);

    }
    void NodeLen(LengthNode leng)
    {
        switch (leng)
        {
            case LengthNode.Harf:
                ChangeImage(Edi_Score.NodePrehab, Nodesprites[0]);
                LenNo = 0.5f;
                break;
            case LengthNode.One:
                ChangeImage(Edi_Score.NodePrehab, Nodesprites[1]);
                LenNo = 1f;
                break;
            case LengthNode.Two:
                ChangeImage(Edi_Score.NodePrehab, Nodesprites[2]);
                LenNo = 2f;
                break;
            case LengthNode.all:
                ChangeImage(Edi_Score.NodePrehab, Nodesprites[3]);
                LenNo = Edi_Score.beat;
                break;
        }
        Edi_Score.nodeLength = LenNo;
    }
    public void ChangeImage(RectTransform rt, Sprite newSprite)
    {
        if (rt == null) return;

        // 子階層から Image コンポーネントを探す（自分含めて）
        var img = rt.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.sprite = newSprite;
        }
    }
}
