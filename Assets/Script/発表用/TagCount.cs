
using System;

[Serializable]
public class HintSelectionState
{
    public Level4 move = Level4.None;
    public Level4 density = Level4.None;
    public Level4 order = Level4.None;
    public Level4 rhythm = Level4.None;

    public Emo5 maxEmo = Emo5.None;
    public Emo5 minEmo = Emo5.None;

    public int CountNonNone()
    {
        int c = 0;
        if (move != Level4.None) c++;
        if (density != Level4.None) c++;
        if (order != Level4.None) c++;
        if (rhythm != Level4.None) c++;
        if (maxEmo != Emo5.None) c++;
        if (minEmo != Emo5.None) c++;
        return c;
    }
}
public enum HintKey
{   Move,Density,Order,Rhythm,MaxEmo,MinEmo }
public enum Level4
{ None, Low, Middle, High }

public enum Emo5
{Sad,Fear,Anxiety,Trust,None }