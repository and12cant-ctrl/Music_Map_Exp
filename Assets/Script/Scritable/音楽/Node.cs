using UnityEngine;

public class Node : ScriptableObject
{
    public int _scale;
    public int _beats;
    public Part _Part;
}

public enum Part
{
    A, B, C,D
}