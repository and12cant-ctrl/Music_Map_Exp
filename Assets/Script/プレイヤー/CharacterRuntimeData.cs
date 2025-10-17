using System.Collections.Generic;

public class CharacterRuntimeData 
{
    public CharaData baseData;
    public string charaName;
    #region ステータス
    public int currentMaxHP;
    public int currentMaxMP;
    public int currentMaxSP;
    public int currentHP;
    public int currentMP;
    public int currentSP;
    public int curAPI;
    public int curStr;
    public int curInt;
    public int currentLv;
    #endregion

    public bool hakenCan = true;

    public List<ActiveStatusEffect> activeEffects = new();

    public CharacterRuntimeData(CharaData baseData)
    {
        this.baseData = baseData;
        charaName = baseData._jobName;
        #region 最大値
        currentMaxHP = baseData._maxHP;
        currentMaxMP = baseData._maxMP;
        currentMaxSP = baseData._maxSP;
        #endregion
        #region 現在値
        currentHP = currentMaxHP;
        currentMP = currentMaxMP;
        currentSP = currentMaxSP;
        #endregion
        currentLv = baseData._level;
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