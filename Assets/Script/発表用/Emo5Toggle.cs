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

    public bool IsWildcard(HintKey key)
    {
        return key switch
        {
            HintKey.Move => move == Level4.None,
            HintKey.Density => density == Level4.None,
            HintKey.Order => order == Level4.None,
            HintKey.Rhythm => rhythm == Level4.None,
            HintKey.MaxEmo => maxEmo == Emo5.None,
            HintKey.MinEmo => minEmo == Emo5.None,
            _ => true
        };
    }
    public bool TryGetLevel4(HintKey key, out Level4 value)
    {
        switch (key)
        {
            case HintKey.Move: value = move; return true;
            case HintKey.Density: value = density; return true;
            case HintKey.Order: value = order; return true;
            case HintKey.Rhythm: value = rhythm; return true;
            default: value = default; return false;
        }
    }
    public bool TryGetEmo5(HintKey key, out Emo5 value)
    {
        switch (key)
        {
            case HintKey.MaxEmo: value = maxEmo; return true;
            case HintKey.MinEmo: value = minEmo; return true;
            default: value = default; return false;
        }
    }
}
public enum HintKey
{   Move,Density,Order,Rhythm,MaxEmo,MinEmo }
public enum Level4
{ None, Low, Mid, High }

public enum Emo5
{None, Sad,Fear,Anxiety,Trust}