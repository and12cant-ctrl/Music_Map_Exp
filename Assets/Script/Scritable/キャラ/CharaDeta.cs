using UnityEngine;
[CreateAssetMenu(fileName = "NewCharacter", menuName = "RPG/Character Data")]
public class CharaData : ScriptableObject
{
    #region ステータス
    public string _jobName;

    public int _defoMaxHP;
    public int _defoMaxMP;
    public int _defoMaxSP;
    public int _defStr;
    public int _defInt;
    public int _defApi;
    public int _defSoc; //社交性
    public int _defSurv; //生存性
    public int _defMys; //神秘
    public bool exit = true;
    #endregion

    public Sprite _characterGraphic;
    public Sprite _charaIcon;

    public SkillData _skill;
    public MagicData[] _magics;
    public Instrument instr ;


}

public enum Instrument
{
    
}