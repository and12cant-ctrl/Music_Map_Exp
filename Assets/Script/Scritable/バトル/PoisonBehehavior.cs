using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Status Behavior/Poison")]
public class PoisonBehavior : StatusEffectBehaviorBase
{
    public int damagePerTurn = 10;

    public override void OnApply(CharacterRuntimeData target)
    {
        Debug.Log($"{target.baseData._jobName} は毒状態になった！");
    }

    public override void OnTurnStart(CharacterRuntimeData target)
    {
        target.currentHP -= damagePerTurn;
        Debug.Log($"{target.baseData._jobName} は毒で {damagePerTurn} ダメージ！");
    }

    public override void OnRemove(CharacterRuntimeData target)
    {
        Debug.Log($"{target.baseData._jobName} の毒が解除された！");
    }
}