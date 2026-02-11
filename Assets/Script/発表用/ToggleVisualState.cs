using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ToggleVisualState : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image background;

     private Color selectedBg = new Color32(255, 0, 0, 150);

    private Color normalBg = new Color32(255,255,255,255);


    void Reset()
    {
        toggle = GetComponent<Toggle>();
        background = GetComponentInChildren<Image>();
    }

    void Awake()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        if (!background) background = GetComponent<Image>();

        // Toggle‘¤‚ÌŽ©“®F•ÏX‚Æ‹£‡‚³‚¹‚È‚¢
        toggle.transition = Selectable.Transition.None;

        toggle.onValueChanged.AddListener(_ => Refresh());
        Refresh();
    }

    void OnEnable() => Refresh();

    public void Refresh()
    {
        bool on = toggle.isOn;
        if (background) background.color = on ? selectedBg : normalBg;
    }
}
