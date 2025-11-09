using UnityEngine;

public abstract class StatusEffectBehaviorBase : ScriptableObject
{
    public abstract void OnApply(CharacterRuntimeData target);
    public abstract void OnTurnStart(CharacterRuntimeData target);
    public abstract void OnRemove(CharacterRuntimeData target);
}
