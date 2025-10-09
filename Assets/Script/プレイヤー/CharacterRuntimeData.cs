using System.Collections.Generic;

public class CharacterRuntimeData 
{
    public CharaData baseData;
    public string charaName;
    public int currentLv;
    public int currentHP;
    public int currentMP;
    public int currentSP;
    public int currentMaxHP;
    public int currentMaxMP;
    public int currentMaxSP;

    public List<ActiveStatusEffect> activeEffects = new();

    public CharacterRuntimeData(CharaData baseData)
    {
        this.baseData = baseData;
        charaName = baseData._jobName;
        #region ç≈ëÂíl
        currentMaxHP = baseData._maxHP;
        currentMaxMP = baseData._maxMP;
        currentMaxSP = baseData._maxSP;
        #endregion
        #region åªç›íl
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