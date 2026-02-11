using UnityEngine;

[CreateAssetMenu(menuName = "MusicMapExp", fileName = "AnswerSet_")]
public class AnswerSet : ScriptableObject
{
    [Header("4 Hints")]
    public Level4 move = Level4.None;
    public Level4 density = Level4.None;
    public Level4 order = Level4.None;
    public Level4 rhythm = Level4.None;

    [Header("2 Emotions")]
    public Emo5 maxEmo = Emo5.None;
    public Emo5 minEmo = Emo5.None;

    public Level4 GetHint(HintKey key) => key switch
    {
        HintKey.Move => move,
        HintKey.Density => density,
        HintKey.Order => order,
        HintKey.Rhythm => rhythm,
        _ => Level4.None
    };


}