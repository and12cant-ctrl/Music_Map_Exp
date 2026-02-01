using UnityEngine;

public class Searize : ScriptableObject
{
    public int HintNum = 3;
    public motion_band motion;
    public density_band density;
    public order_band order;
    public rhythm_band rhythm;
    public Emotion Highest_Emo;
    public Emotion Lowest_Emo;
}
public enum motion_band
{
    Low,Medium,High
}
public enum density_band
{
    Low, Medium, High
}
public enum order_band
{
    Low, Medium, High
}
public enum rhythm_band
{
    Low, Medium, High
}

public enum Emotion
{
    trust,sad,fear,anxiety
}