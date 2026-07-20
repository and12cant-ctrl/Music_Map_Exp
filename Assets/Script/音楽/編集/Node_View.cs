using UnityEngine;
using UnityEngine.UI;

public class Node_View : MonoBehaviour
{
    public Part Part { get; private set; }

    private Image image;

    void Awake()
    {
        image = GetComponentInChildren<Image>();
    }

    public void Init(Part part)
    {
        Part = part;
    }

    public void SetAlpha(float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
