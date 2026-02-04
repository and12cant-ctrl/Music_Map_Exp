using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartBoxUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private GameObject GameObject;
    private PartSelect select;
    private Part _part;
    public void Init(PartSelect Sel, Part part)
    {
        _part = part;
        _text.text = part.ToString();
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() =>
        {
            Sel.img.color = PartColorUtil.GetNodeColor(part);
            Sel.Nodesele(part);
            Sel.TabClose.CloseCurrentTab();
        });

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
            _ => Color.black
        };
    }
}