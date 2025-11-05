using UnityEngine;
[CreateAssetMenu(fileName = "NewCharacter", menuName = "RPG/Character Data")]
public class CharaData : ScriptableObject
{
    #region ステータス
    public string _jobName;
    public int _level;
    public int _defoMaxHP;
    public int _defoMaxMP;
    public int _defoMaxSP;
    public int _defoStr;
    public int _defoInt;
    public int _defoApi;
    public bool exit = true;
    #endregion

    public Sprite _characterGraphic;

    public SkillData[] _skills;
    public MagicData[] _magics;
    public instrument instr ;
}

public enum instrument
{
    
}