using UnityEngine;
using UnityEngine.UI;

public sealed class SimpleStaffRenderer : MonoBehaviour, IStaffRenderer
{
    [SerializeField] private RectTransform exBox;
    [SerializeField] public RectTransform nodePrefab;

    public void RenderPlace(ScoreEvent e, Vector2 anchoredPos, string nodeName)
    {
        RectTransform note = Instantiate(nodePrefab, exBox); 
        Image img = nodePrefab.GetComponentInChildren<Image>();
        img.color = PartColorUtil.GetNodeColor(e.part);
        note.anchoredPosition = anchoredPos;
        note.name = nodeName;
    }

    public void RenderDelete(ScoreEvent e, Vector2 anchoredPos)
    {
        float eps = 0.1f;
        foreach (RectTransform child in exBox)
        {
            if (Vector2.Distance(child.anchoredPosition, anchoredPos) < eps)
            {
                Destroy(child.gameObject);
                return;
            }
        }
    }
}
