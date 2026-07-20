using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "RPG/Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public string description;
    public int PlayStack;
    public CardType Type;
    public int SPEfecct;
}

