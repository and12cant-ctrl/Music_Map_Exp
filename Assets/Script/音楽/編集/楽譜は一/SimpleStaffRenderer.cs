using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SimpleStaffRenderer : MonoBehaviour, IStaffRenderer
{
    //[SerializeField] private RectTransform exBox;
    [SerializeField] public RectTransform nodePrefab;
    [SerializeField] public RectTransform ABox;
    [SerializeField] public RectTransform BBox;
    [SerializeField] public RectTransform CBox;
    [SerializeField] public RectTransform DBox;
    [SerializeField] private float selectedAlpha = 1f;
    [SerializeField] private float unselectedAlpha = 0.25f;

    public Dictionary<Part, RectTransform> partRoots = new();
    private void Start()
    {
        Image img = nodePrefab.GetComponentInChildren<Image>();
        img.color = Color.white;
        Dictionary<Part, RectTransform> partRoots = new()
        {
            { Part.A, ABox },
            { Part.B, BBox },
            { Part.C, CBox },
            { Part.D, DBox }
        };
    }

    public void RenderPlace(ScoreEvent e, Vector2 anchoredPos, string nodeName)
    {
        Image img = nodePrefab.GetComponentInChildren<Image>();
        img.color = PartColorUtil.GetNodeColor(e.part);
        RectTransform note = Instantiate(nodePrefab, SelPoket(e.part)); 

        note.anchoredPosition = anchoredPos;
        note.name = nodeName;
    }

    public void RenderDelete(ScoreEvent e, Vector2 anchoredPos)
    {
        float eps = 10f;
        foreach (RectTransform child in SelPoket(e.part))
        {
            if (Vector2.Distance(child.anchoredPosition, anchoredPos) < eps)
            {
                Destroy(child.gameObject);
                return;
            }
        }
    }

    public RectTransform SelPoket(Part par)
    {
        switch (par)
        {
            case Part.A:
                return ABox;
            case Part.B:
                return BBox;
            case Part.C:
                return CBox;
            case Part.D:
                return DBox;
        }
        return null;
    }

    public void BringFrontPart(Part selected)
    {
        RectTransform t = SelPoket(selected);
        if (t == null) return;
        Debug.Log("Part‚ªNULL");
        t.SetAsLastSibling();
    }

}
