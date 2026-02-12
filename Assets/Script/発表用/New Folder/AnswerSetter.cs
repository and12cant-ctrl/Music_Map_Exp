using UnityEngine;

public class AnswerSetter : MonoBehaviour
{
    [SerializeField] private MusicXmlEditorCaller Caller;

    [Header("Hint Views")]
    [SerializeField] private Level4ToggleGroupView move;
    [SerializeField] private Level4ToggleGroupView density;
    [SerializeField] private Level4ToggleGroupView order;
    [SerializeField] private Level4ToggleGroupView rhythm;

    [SerializeField] private Emo5ToggleGroupView maxEmo;
    [SerializeField] private Emo5ToggleGroupView minEmo;
    private AnswerSet answer;

    private void Start()
    {
        answer = Caller.Answer;
        Apply();

    }

    public void Apply()
    {
        if (!answer) return;

        move.Apply(answer.move);
        density.Apply(answer.density);
        order.Apply(answer.order);
        rhythm.Apply(answer.rhythm);

        maxEmo.Apply(answer.maxEmo);
        minEmo.Apply(answer.minEmo);
    }
}
