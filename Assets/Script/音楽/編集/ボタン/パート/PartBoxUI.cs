using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartBoxUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private GameObject GameObject;

    private Part _part;
    public void Init(PartSelect Sel, Part part)
    {
        _part = part;
        _text.text = part.ToString();
        // ŒÃ‚¢ƒŠƒXƒi[‚ðÁ‚µ‚Ä‚©‚ç“o˜^i”O‚Ì‚½‚ßj
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() =>
        {

            Sel.Nodesele(part);
        });

    }
}
