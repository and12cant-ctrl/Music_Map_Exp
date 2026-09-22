using System.Collections.Generic;

public class PartyRuntime
{
    public List<CharacterRuntimeData> Members = new();

    public List<HintData> Hints = new();
    public List<EquipData> Equips = new();
    public List<AbilityData> Abilities = new();

    public int Gold;
    public int Turn;
    public int PartyMemberNum = 3;
}
