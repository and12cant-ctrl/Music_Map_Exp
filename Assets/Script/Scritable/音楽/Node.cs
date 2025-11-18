using UnityEngine;

public class Node 
{
    public int _scale;
    public int _beats;
    public float _duration;

    public Part _Part;
    public string pitch;
    public float starttime;
}

public enum Part
{
    A, B, C,D
}

