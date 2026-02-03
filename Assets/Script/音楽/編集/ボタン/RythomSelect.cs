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
    private int sizeY = 10;
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
        ChangeImage(Edi_Score.NodePrehab, 1);

    }
    void NodeLen(LengthNode leng)
    {
        switch (leng)
        {
            case LengthNode.Harf:
                ChangeImage(Edi_Score.NodePrehab,1);
                LenNo = 0.5f;
                TabClose.CloseCurrentTab();
                break;
            case LengthNode.One:
                ChangeImage(Edi_Score.NodePrehab,2);
                LenNo = 1f;
                TabClose.CloseCurrentTab();
                break;
            case LengthNode.Two:
                ChangeImage(Edi_Score.NodePrehab, 4);
                LenNo = 2f;
                TabClose.CloseCurrentTab();
                break;
            case LengthNode.all:
                ChangeImage(Edi_Score.NodePrehab, 8);
                LenNo = Edi_Score.beat;
                TabClose.CloseCurrentTab();
                break;
        }
        Edi_Score.nodeLength = LenNo;
    }
    public void ChangeImage(RectTransform rt, int n)
    {
        if (rt == null) return;

        // 子階層から Image コンポーネントを探す（自分含めて）
        var img = rt.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.rectTransform.sizeDelta =new Vector2(60 * n,sizeY);
;        }
    }
}
