using UnityEngine;
using UnityEngine.UI;
using System;

public class Emo5ToggleGroupView : MonoBehaviour
{
    public Toggle none, trust, sad, fear, anxiety;
    public bool suppress;

    public event Action<Emo5> OnUserChanged;

    void Awake()
    {
        Wire(none, Emo5.None);
        Wire(trust, Emo5.Trust);
        Wire(sad, Emo5.Sad);
        Wire(fear, Emo5.Fear);
        Wire(anxiety, Emo5.Anxiety);
    }

    void Wire(Toggle t, Emo5 v)
    {
        t.onValueChanged.AddListener(isOn =>
        {
            if (suppress) return;
            if (!isOn) return;
            OnUserChanged?.Invoke(v);
        });
    }

    public void Apply(Emo5 v)
    {
        suppress = true;
        none.isOn = (v == Emo5.None);
        trust.isOn = (v == Emo5.Trust);
        sad.isOn = (v == Emo5.Sad);
        fear.isOn = (v == Emo5.Fear);
        anxiety.isOn = (v == Emo5.Anxiety);
        suppress = false;
    }
}
