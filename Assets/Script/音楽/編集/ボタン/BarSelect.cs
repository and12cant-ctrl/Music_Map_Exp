using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BarSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private RectTransform score;
    [SerializeField] private List<Toggle> toggles = new();
    private int ScoreWidth = -330;
    private void Awake()
    {
        for (int i = 0; i < toggles.Count; i++)
        {
            int index = i;
            Toggle toggle = toggles[i];
            if (toggle == null) continue;

            toggle.onValueChanged.AddListener(isOn =>
            {
                Debug.Log(index);
                Barsele(index);
     
            });
        }
    }

    void Barsele(int Bar)
    {

        Edi_Score.currentBar = Bar;
        score.anchoredPosition = new Vector2 ((Bar)* ScoreWidth,0);
    }
}
