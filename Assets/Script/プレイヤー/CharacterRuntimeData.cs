using System.Collections.Generic;
using UnityEngine;

public class CharacterRuntimeData 
{
    public CharaData baseData;
    public string charaName;
    public Sprite Icon;
    public Sprite charaGraphic;

    public SkillData _skill;
    public MagicData[] _magics;
    public Instrument instr;
    #region ステータス
    public int currentMaxHP;
    public int currentMaxMP;
    public int currentMaxSP;
    public int currentHP;
    public int currentMP;
    public int currentSP;

    public int curStr;
    public int curInt;
    public int curApi;
    public int curSoc;
    public int curSur;
    public int curMys; 
    public int currentLv;
    #endregion

    public bool hakenCan = true;

    public List<ActiveStatusEffect> activeEffects = new();

    public CharacterRuntimeData(CharaData baseData)
    {
        this.baseData = baseData;
        charaName = baseData._jobName;
        #region 最大値
        currentMaxHP = baseData._defoMaxHP;
        currentMaxMP = baseData._defoMaxMP;
        currentMaxSP = baseData._defoMaxSP;
        #endregion
        #region 現在値
        currentHP = currentMaxHP;
        currentMP = currentMaxMP;
        currentSP = currentMaxSP;
        #endregion
        #region ステータス
        curStr = baseData._defStr;
        curInt = baseData._defInt;
        curApi = baseData._defApi;
        curSoc = baseData._defSoc;
        curSur = baseData._defSurv;
        curMys = baseData._defMys;
        Icon = baseData._charaIcon;
        charaGraphic = baseData._characterGraphic;
        #endregion

        #region
        _skill = baseData._skill;
        _magics = baseData._magics;
        #endregion
    }

    public string skillName()
    {
        return _skill.skillName[..2];
    }

    public int skillcnt()
    {
        return _skill.PlayStack;
    }
    public CardType GetSkilltype()
    {
        return _skill.Type;
    }

    public Sprite GetIcon()
    {
        return Icon;
    }
}

public class ActiveStatusEffect
{
    public StatusEffectData effectData;
    public int remainingTurn;

    public ActiveStatusEffect(StatusEffectData data)
    {
        effectData = data;
        remainingTurn = data.duration;
    }
}