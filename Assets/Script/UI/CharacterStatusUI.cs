using TMPro;
using UnityEngine;

public class CharacterStatusUI : MonoBehaviour
{
    private CharacterRuntimeData _chara;
    [SerializeField] private TMP_Text _HPtext;
    [SerializeField] private TMP_Text _MPtext;
    [SerializeField]private TMP_Text _SPtext;
    [SerializeField]private TMP_Text _NameText;
    [SerializeField] private TMP_Text _Leveltext;
    [SerializeField] public CardCharaManeger CharaMan;

    public void Setup(CharacterRuntimeData chara)
    {
        _chara = chara;
        gameObject.SetActive(_chara != null);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_chara == null || _chara == null || _chara.baseData == null)
            return;
        _NameText.text = $"{_chara.charaName}";
        _HPtext.text = $"HP {_chara.currentHP}/{_chara.currentMaxHP}";
        _MPtext.text = $"MP {_chara.currentMP}/{_chara.currentMaxMP}";
        _SPtext.text = $"SP {_chara.currentSP}/{_chara.currentMaxMP}";
        _Leveltext.text = $"Lv {_chara.currentLv}";
    }

}