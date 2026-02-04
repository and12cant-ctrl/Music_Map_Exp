using UnityEngine;
using UnityEngine.UI;

public class ModeSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private Button btnOpen;
    [SerializeField] private Button btnGene;
    [SerializeField] private Button btnDel;
    [SerializeField] private GameObject partBox;
    [SerializeField] public TabOverLayCloser TabClose;
    void Start()
    {
        btnOpen.onClick.AddListener(() =>
        {
            TabClose.OpenTab(partBox);
        });
        btnGene.onClick.AddListener(() =>
        {
            Nodesele(Node_judge.EditMode.Place);
        });
        btnDel.onClick.AddListener(() =>
        {
            Nodesele(Node_judge.EditMode.Delete);
        });
    }
    void Nodesele(Node_judge.EditMode mode)
    {
        Debug.Log($"[Nodesele] set mode={mode} on Edi_Score instance={Edi_Score?.GetInstanceID()}");
        Edi_Score.editMode = mode;

        TabClose.CloseCurrentTab();
    }

}
