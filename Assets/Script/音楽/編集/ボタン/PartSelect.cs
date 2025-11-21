using UnityEngine;
using UnityEngine.UI;

public class PartSelect : MonoBehaviour
{
    [SerializeField] private Nod_judge Edi_Score;
    [SerializeField] private Button GeneNode;
    [SerializeField] private Button DeleNode;
    void Start()
    {
        GeneNode.onClick.AddListener(() =>
        {
            Nodesele();
        });
        DeleNode.onClick.AddListener(() =>
        {
            NodeDele();
        });
    }
    void Nodesele()
    {
        Edi_Score.editMode = Nod_judge.EditMode.Place;
    }
    void NodeDele()
    {
        Edi_Score.editMode = Nod_judge.EditMode.Delete;
    }

}
