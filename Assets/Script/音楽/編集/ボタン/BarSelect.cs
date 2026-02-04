using UnityEngine;
using UnityEngine.UI;

public class BarSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private RectTransform score;
    [SerializeField] private Button btnOne;
    [SerializeField] private Button btnTwo;
    [SerializeField] private Button btnThree;
    [SerializeField] private Button btnFour;
    void Start()
    {
        btnOne.onClick.AddListener(() =>
        {
            Barsele(1);
        });
        btnTwo.onClick.AddListener(() =>
        {
            Barsele(2);
        });
        btnThree.onClick.AddListener(() =>
        {
            Barsele(3);
        });
        btnFour.onClick.AddListener(() =>
        {
            Barsele(4);
        });
    }
    void Barsele(int Bar)
    {
        Edi_Score.currentBar = Bar;
        score.anchoredPosition = new Vector2 ((Bar - 1 )* -330,5);
    }
}
