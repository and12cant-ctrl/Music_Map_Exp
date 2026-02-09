using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TagCount : MonoBehaviour
{

}
public enum Hint
{
    move, density, order, rhythm, MaxEmo, MinEmo
}
public enum Scale
{
    çÇ,íÜ,í·
}

public class HintLevels
{
    public Scale move;
    public Scale density;
    public Scale order;
    public Scale rhythm;
    public Scale emotion;
    public int Count;
}