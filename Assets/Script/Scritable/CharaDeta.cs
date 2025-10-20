using UnityEngine;
[CreateAssetMenu(fileName = "NewCharacter", menuName = "RPG/Character Data")]
public class CharaData : ScriptableObject
{
    #region ステータス
    public string _jobName;
    public int _level;
    public int _maxHP;
    public int _maxMP;
    public int _maxSP;
    public int _str;
    public int _int;
    public int _api;
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