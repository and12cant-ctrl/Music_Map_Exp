using UnityEngine;
using UnityEngine.UI;

public class ModeSelect : MonoBehaviour
{
    [SerializeField] private Node_judge Edi_Score;
    [SerializeField] private Button btnGene;
    [SerializeField] private Button btnDel;
    void Start()
    {
        btnGene.onClick.AddListener(() =>
        {
            Nodesele(Node_judge.EditMode.Place);
        });
        btnDel.onClick.AddListener(() =>
        {
            Nodesele(Node_judge.EditMode.Place);
        });
    }
    void Nodesele(Node_judge.EditMode mode)
    {
        Edi_Score.editMode = mode;
    }

}
