using UnityEngine;
using UnityEngine.UI;

public class PartSelect : MonoBehaviour
{
    [SerializeField] public Node_judge_re Edi_Score;
    [SerializeField] public GameManager gameManager;
    [SerializeField] private GameObject partPrehabBox;
    [SerializeField] public GameObject partBox;
    [SerializeField] private Button GeneButton;
    [SerializeField] private RectTransform Node;
    [SerializeField] public TabOverLayCloser TabClose;
    [HideInInspector] public Image img;
    void Start()
    {
        GeneButton.onClick.RemoveAllListeners();

        img = Node.GetComponentInChildren<Image>();
        GeneButton.onClick.AddListener(() =>
        {
            TabClose.OpenTab(partBox); 
        });
    }
    public void Nodesele(Part part)
    {
        Edi_Score.part = part;
    }
}

public static class PartColorUtil
{
    public static Color GetNodeColor(Part part)
    {
        return part switch
        {
            Part.A => Color.white,
            Part.B => Color.red,
            Part.C => Color.blue,
            _ => Color.green
        };
    }
}
