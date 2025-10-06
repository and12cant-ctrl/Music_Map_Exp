using System.Collections.Generic;

public class CharacterRuntimeData 
{
    public CharaData baseData;
    public string charaName;
    public int currentLv;
    public int currentHP;
    public int currentMP;
    public int currentSP;

    public List<ActiveStatusEffect> activeEffects = new();

    public CharacterRuntimeData(CharaData baseData)
    {
        this.baseData = baseData;
        charaName = baseData._jobName;
        currentHP = baseData._maxHP;
        currentMP = baseData._maxMP;
        currentSP = baseData._maxSP;
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