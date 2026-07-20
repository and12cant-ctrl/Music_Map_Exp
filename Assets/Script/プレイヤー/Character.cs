using UnityEngine;

public class Character : MonoBehaviour
{
    public CharaData chara;
    public CharacterRuntimeData runtime;

    public void Start()
    {
        Setup(chara);
    }
    public void Setup(CharaData baseData)
    {
        runtime = new CharacterRuntimeData(baseData);
    }
    public string SkillName()
    {
        return chara._skill.skillName[..2];
    }

    public int Skillcnt()
    {
        return chara._skill.PlayStack;
    }
    public CardType GetSkilltype()
    {
        return chara._skill.Type;
    }

    public Sprite GetIcon()
    {
        return chara._charaIcon;
    }
}
