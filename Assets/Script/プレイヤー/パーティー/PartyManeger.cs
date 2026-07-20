using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public PartyRuntime Runtime { get; private set; }

    private void Awake()
    {
        Runtime = new PartyRuntime();
    }

    public void AddMember(CharacterRuntimeData member)
    {
        Runtime.Members.Add(member);
    }

    public void RemoveMember(CharacterRuntimeData member)
    {
        Runtime.Members.Remove(member);
    }

    public void AddGold(int amount)
    {
        Runtime.Gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (Runtime.Gold < amount)
            return false;

        Runtime.Gold -= amount;
        return true;
    }

    public void NextTurn()
    {
        Runtime.Turn++;
    }

    public void AddAbility(AbilityData ability)
    {
        Runtime.Abilities.Add(ability);
    }

    public void AddEquip(EquipData equip)
    {
        Runtime.Equips.Add(equip);
    }

    public void AddHint(HintData hint)
    {
        Runtime.Hints.Add(hint);
    }
}