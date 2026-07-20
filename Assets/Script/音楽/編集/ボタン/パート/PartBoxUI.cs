using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartToggleGroupController : MonoBehaviour
{
    [SerializeField] private List<Toggle> toggles = new();
    [SerializeField] private PartSelect owner;

    private void Awake()
    {
        owner.Edi_Score.staffRenderer.BringFrontPart(Part.A);
        for (int i = 0; i < toggles.Count; i++)
        {
            bool active = i < owner.gameManager.PartyManager.Runtime.Members.Count;
            toggles[i].gameObject.SetActive(active);
            if (!active) continue;

            int index = i;
            Toggle toggle = toggles[i];
            if (toggle == null) continue;

            toggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;

                Part UIpart = (Part)index;
                owner.Edi_Score.part = UIpart;
                owner.Edi_Score.staffRenderer.BringFrontPart(UIpart);
                owner.TabClose.CloseCurrentTab();
            });
        }
    }
}