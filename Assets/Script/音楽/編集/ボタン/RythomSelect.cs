using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RythomSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private Button OpenTab;
    [SerializeField] private GameObject RhyhomTab;
    [SerializeField] private List<Toggle> toggles = new();
    private int sizeY = 10;
    private float LenNo = 0.5f;
    void Start()
    {
        OpenTab.onClick.AddListener(() =>
        {
            Edi_Score._closer.OpenTab(RhyhomTab);
        });

        for (int i = 0; i < toggles.Count; i++)
        {
            int index = i;
            Toggle toggle = toggles[i];
            if (toggle == null) continue;

            toggle.onValueChanged.AddListener(isOn =>
            {
                NodeLen(index);

               Edi_Score._closer.CloseCurrentTab();
            });
        }
        ChangeImage(Edi_Score.NodePrehab, 1);
    }
    

    void NodeLen(int leng)
    {
        switch (leng)
        {
            case 0:
                ChangeImage(Edi_Score.NodePrehab,1);
                LenNo = 0.5f;

                break;
            case 1:
                ChangeImage(Edi_Score.NodePrehab,2);
                LenNo = 1f;
                break;
            case 2:
                ChangeImage(Edi_Score.NodePrehab, 4);
                LenNo = 2f;
                break;
            case 3:
                ChangeImage(Edi_Score.NodePrehab, 8);
                LenNo = Edi_Score.beat;
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
            //Debug.Log("Tes");
            //img.rectTransform.sizeDelta =new Vector2(60 * n,sizeY);
            //Debug.Log("img name = " + img.name);
            //Debug.Log("img size before = " + img.rectTransform.sizeDelta);

            img.rectTransform.sizeDelta = new Vector2(60 * n, sizeY);

            //Debug.Log("img size after = " + img.rectTransform.sizeDelta);
            //Debug.Log("rt size after = " + rt.sizeDelta);
        }
    }

}
