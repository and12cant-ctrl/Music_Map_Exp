using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Node_judge_re;

public class ModeSelect : MonoBehaviour
{
    [SerializeField] private Node_judge_re Edi_Score;
    [SerializeField] private Button btnOpen;
    [SerializeField] private GameObject partBox;
    [SerializeField] private List<Toggle> toggles = new();
    void Start()
    {


    }

    private void Awake()
    {
        btnOpen.onClick.AddListener(() =>
        {
            Edi_Score._closer.OpenTab(partBox);
        });
        toggles[0].onValueChanged.AddListener(isOn =>
        {
            if (!isOn) return;

            Nodesele(EditMode.Place);
        });
        toggles[1].onValueChanged.AddListener(isOn =>
        {
            if (!isOn) return;

            Nodesele(EditMode.Delete);
        });

    }
    void Nodesele(EditMode mode)
    {
        Debug.Log($"[Nodesele] set mode={mode} on Edi_Score instance={Edi_Score?.GetInstanceID()}");
        Edi_Score.editMode = mode;

        Edi_Score._closer.CloseCurrentTab();
    }

}
