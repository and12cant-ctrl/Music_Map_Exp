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
    public voice _voice;

    public bool OverlapsIgnoreVoice(Node other)
    {
        if (_Part != other._Part) return false;
        if (_pitch != other._pitch) return false;
        if (_bar != other._bar) return false;

        float a0 = _startBar;
        float a1 = _startBar + _duration;
        float b0 = other._startBar;
        float b1 = other._startBar + other._duration;

        return Mathf.Max(a0, b0) < Mathf.Min(a1, b1);
    }
}

public enum Part
{
    A, B, C,D
}
public enum voice
{
    Up,Middle,Down
}
