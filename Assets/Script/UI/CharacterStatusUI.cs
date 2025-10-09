using System;
using TMPro;
using UnityEngine;

public class CharacterStatusUI : MonoBehaviour
{
    [SerializeField] private Character _chara;
    [SerializeField] private TMP_Text _HPtext;
    [SerializeField] private TMP_Text _MPtext;
    [SerializeField]private TMP_Text _SPtext;
    [SerializeField]private TMP_Text _NameText;
    [SerializeField] private TMP_Text _Leveltext;

    public void Setup(Character chara)
    {
        _chara = chara;
        gameObject.SetActive(_chara != null);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_chara == null || _chara.runtime == null || _chara.runtime.baseData == null)
            return;
        _NameText.text = $"{_chara.runtime.charaName}";
        _HPtext.text = $"{_chara.runtime.currentHP}/{_chara.runtime.currentMaxHP}";
        _MPtext.text = $"{_chara.runtime.currentMP}/{_chara.runtime.currentMaxMP}";
        _SPtext.text = $"{_chara.runtime.currentSP}/{_chara.runtime.baseData._maxSP}";
        _Leveltext.text = $"Lv {_chara.runtime.currentLv}";
    }

}