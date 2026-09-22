using System.Collections.Generic;
using TMPro;
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
            Toggle toggle = toggles[i];
            if (toggle == null) continue;

            int index = i;
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

    private void OnEnable()
    {
        List<CharacterRuntimeData> members = owner.gameManager.PartyManager.Runtime.Members;

        for (int i = 0; i < toggles.Count; i++)
        {
            Toggle toggle = toggles[i];
            if (toggle == null) continue;

            bool active = i < members.Count;
            toggle.gameObject.SetActive(active);
            if (!active) continue;

            TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = members[i].charaName;
            }
        }
    }
}
