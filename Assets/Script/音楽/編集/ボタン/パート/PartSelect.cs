using UnityEngine;
using UnityEngine.UI;

public class PartSelect : MonoBehaviour
{
    [SerializeField] private Node_judge Edi_Score;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject partPrehabBox;
    [SerializeField] private GameObject partBox;
    [SerializeField] private Button GeneButton;
    [SerializeField] private RectTransform Node;
    TabOverLayCloser TabClose;
    public Image img;
    void Start()
    {
        GeneButton.onClick.RemoveAllListeners();
        SpawnPartBoxes();
        img = Node.GetComponentInChildren<Image>();
        GeneButton.onClick.AddListener(() =>
        {
            TabClose.OpenTab(partBox);
        });
        //GeneButton.onClick.AddListener(() =>
        //{
        //    Clicker();
        //});
    }
    public void Nodesele(Part part)
    {
        Edi_Score.part = part;
        Debug.Log(Edi_Score.part);
    }
    private void SpawnPartBoxes()
    {
        RectTransform InstRt = partBox.GetComponent<RectTransform>();
        InstRt.anchoredPosition = new Vector2(15,60);
        for (int i = 0; i < gameManager.characterCount; i++)
        {
            // インデックスから Part を決める（人数が4を超える場合は適宜調整）
            Part part = (Part)i;

            GameObject obj = Instantiate(partPrehabBox, InstRt);
            obj.GetComponent<PartSelect>();
            RectTransform rt  = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-12,37-(i*20));
            PartBoxUI ui = obj.GetComponent<PartBoxUI>();
            ui.Init(this, part);
        }
    }
    private void Clicker()
    {
        partBox.SetActive(true);
    }
    public void UnClicer()
    {
        partBox.SetActive(false);
    }

}
