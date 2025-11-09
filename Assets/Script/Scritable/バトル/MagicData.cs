using UnityEngine;

[CreateAssetMenu(fileName = "NewMagic", menuName = "RPG/Magic")]
public class MagicData : ScriptableObject
{
    public string magicName;
    public string description;
    public int mpCost;
    public int power;
    public ElementType element;
}

public enum ElementType
{
    Fire,
    Water,
    Wind,
    Earth,
    Light,
    Dark
}
