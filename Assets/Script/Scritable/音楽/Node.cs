using System;
using UnityEngine;
[Serializable]
public class Node 
{

    public float _duration;     //何拍続くか
    public Part _Part;          //誰のパートか
    public string _pitch;       //どの音階か
    public float _startBar;    //何拍目においてあるか
    public int _bar;            //何小節目か
    public bool _harmony;
}

public enum Part
{
    A, B, C,D
}

