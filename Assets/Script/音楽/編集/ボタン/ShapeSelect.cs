using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;
using static Node_judge_re;

public class ShapeSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private Button btnShape;
    [SerializeField] private Button btnFlat;
    [SerializeField] private Button btnNone;
    void Start()
    {
        btnShape.onClick.AddListener(() =>
            SetAccidental(harfNode.Shape)
        );
        btnFlat.onClick.AddListener(() =>
            SetAccidental(harfNode.Flat)
        );
        btnNone.onClick.AddListener(() =>
            SetAccidental(harfNode.None)
        );
    }
    private void SetAccidental(harfNode accidental)
    {
        Edi_Score.hafeN = accidental;
    }

}
