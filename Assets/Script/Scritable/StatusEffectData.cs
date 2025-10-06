using UnityEngine;

public enum StatusEffectType
{
    Poison,
    Paralysis,
    Sleep,
    Buff,
    Debuff
}

[CreateAssetMenu(menuName = "RPG/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    public string effectName;
    public string description;
    public int duration;
    public Sprite icon;
    public StatusEffectType effectType;
    public StatusEffectBehaviorBase behavior;
}
