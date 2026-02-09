using UnityEngine;
using UnityEngine.UI;
using System;

public class Level4ToggleGroupView : MonoBehaviour
{
    public Toggle none, low, middle, high;
    public bool suppress; // 反映中にイベントが再発火しないように

    public event Action<Level4> OnUserChanged; // ユーザー操作だけ通知

    void Awake()
    {
        Wire(none, Level4.None);
        Wire(low, Level4.Low);
        Wire(middle, Level4.Middle);
        Wire(high, Level4.High);
    }

    void Wire(Toggle t, Level4 v)
    {
        t.onValueChanged.AddListener(isOn =>
        {
            if (suppress) return;
            if (!isOn) return; // ONになった瞬間だけ採用
            OnUserChanged?.Invoke(v);
        });
    }

    public void Apply(Level4 v)
    {
        suppress = true;
        none.isOn = (v == Level4.None);
        low.isOn = (v == Level4.Low);
        middle.isOn = (v == Level4.Middle);
        high.isOn = (v == Level4.High);
        suppress = false;
    }
}
