using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

public class ShapeSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private Button btnShape;
    [SerializeField] private Button btnFlat;
    [SerializeField] private Button btnNone;
    void Start()
    {
        btnShape.onClick.AddListener(() =>
            SetAccidental(Node_judge.harfNode.Shape)
        );
        btnFlat.onClick.AddListener(() =>
            SetAccidental(Node_judge.harfNode.Flat)
        );
        btnNone.onClick.AddListener(() =>
            SetAccidental(Node_judge.harfNode.None)
        );
    }
    private void SetAccidental(Node_judge.harfNode accidental)
    {
        Edi_Score.hafeN = accidental;
    }

}
