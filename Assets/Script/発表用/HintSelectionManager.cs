using System.Collections.Generic;
using UnityEngine;

public class HintSelectionManager : MonoBehaviour
{
    [Header("State (source of truth)")]
    public HintSelectionState state = new HintSelectionState();

    [Header("Views")]
    public Level4ToggleGroupView moveView;
    public Level4ToggleGroupView densityView;
    public Level4ToggleGroupView orderView;
    public Level4ToggleGroupView rhythmView;

    public Emo5ToggleGroupView maxEmoView;
    public Emo5ToggleGroupView minEmoView;

    // 「None以外」の選択履歴（古い順 → 新しい順）
    private readonly List<HintKey> _pickedOrder = new();

    void Awake()
    {
        moveView.OnUserChanged += v => RequestSet(HintKey.Move, v);
        densityView.OnUserChanged += v => RequestSet(HintKey.Density, v);
        orderView.OnUserChanged += v => RequestSet(HintKey.Order, v);
        rhythmView.OnUserChanged += v => RequestSet(HintKey.Rhythm, v);

        maxEmoView.OnUserChanged += v => RequestSet(HintKey.MaxEmo, v);
        minEmoView.OnUserChanged += v => RequestSet(HintKey.MinEmo, v);

        // 初期状態反映（全部NoneでもOK）
        RebuildPickedOrderFromState();
        ApplyAllToUI();
    }

    // ---- RequestSet (overloads) ----
    void RequestSet(HintKey key, Level4 v)
    {
        SetLevel4(key, v);
        EnforceConstraints(key); // ここで自動調整
        ApplyAllToUI();
    }

    void RequestSet(HintKey key, Emo5 v)
    {
        SetEmo5(key, v);
        EnforceConstraints(key); // ここで自動調整
        ApplyAllToUI();
    }

    // ---- Core rules (B仕様) ----
    void EnforceConstraints(HintKey changedKey)
    {
        // 1) MaxEmo と MinEmo 排他：後から触った方を優先して、もう片方をNoneへ
        if (changedKey == HintKey.MaxEmo && state.maxEmo != Emo5.None && state.minEmo != Emo5.None)
        {
            state.minEmo = Emo5.None;
            RemoveFromOrder(HintKey.MinEmo);
        }
        else if (changedKey == HintKey.MinEmo && state.minEmo != Emo5.None && state.maxEmo != Emo5.None)
        {
            state.maxEmo = Emo5.None;
            RemoveFromOrder(HintKey.MaxEmo);
        }

        // 2) 選択履歴の更新（changedKeyがNone以外なら「最新」に、Noneなら履歴から消す）
        if (IsNonNone(changedKey)) TouchAsLatest(changedKey);
        else RemoveFromOrder(changedKey);

        // 3) None以外は最大3つ：超えたら「一番古い選択」をNoneに落とす
        while (state.CountNonNone() > 3)
        {
            if (_pickedOrder.Count == 0) break;

            var oldest = _pickedOrder[0];
            _pickedOrder.RemoveAt(0);

            // 安全のため：changedKeyが最新になっている想定だが、万一 oldest==changedKey なら次へ
            if (oldest == changedKey)
            {
                continue;
            }

            ForceNone(oldest);
        }

        // 4) 念のため履歴と状態の整合性を軽く修正（Noneになったものが残ってたら消す）
        CleanupOrder();
    }

    // ---- helpers: order ----
    void TouchAsLatest(HintKey key)
    {
        _pickedOrder.Remove(key);
        _pickedOrder.Add(key);
    }

    void RemoveFromOrder(HintKey key) => _pickedOrder.Remove(key);

    void CleanupOrder()
    {
        for (int i = _pickedOrder.Count - 1; i >= 0; i--)
        {
            if (!IsNonNone(_pickedOrder[i]))
                _pickedOrder.RemoveAt(i);
        }
    }

    void RebuildPickedOrderFromState()
    {
        _pickedOrder.Clear();
        // 初期は「順序がない」ので固定順で入れる（Noneは入らない）
        if (state.move != Level4.None) _pickedOrder.Add(HintKey.Move);
        if (state.density != Level4.None) _pickedOrder.Add(HintKey.Density);
        if (state.order != Level4.None) _pickedOrder.Add(HintKey.Order);
        if (state.rhythm != Level4.None) _pickedOrder.Add(HintKey.Rhythm);
        if (state.maxEmo != Emo5.None) _pickedOrder.Add(HintKey.MaxEmo);
        if (state.minEmo != Emo5.None) _pickedOrder.Add(HintKey.MinEmo);
    }

    // ---- helpers: state read/write ----
    bool IsNonNone(HintKey key) => key switch
    {
        HintKey.Move => state.move != Level4.None,
        HintKey.Density => state.density != Level4.None,
        HintKey.Order => state.order != Level4.None,
        HintKey.Rhythm => state.rhythm != Level4.None,
        HintKey.MaxEmo => state.maxEmo != Emo5.None,
        HintKey.MinEmo => state.minEmo != Emo5.None,
        _ => false
    };

    void ForceNone(HintKey key)
    {
        switch (key)
        {
            case HintKey.Move: state.move = Level4.None; break;
            case HintKey.Density: state.density = Level4.None; break;
            case HintKey.Order: state.order = Level4.None; break;
            case HintKey.Rhythm: state.rhythm = Level4.None; break;
            case HintKey.MaxEmo: state.maxEmo = Emo5.None; break;
            case HintKey.MinEmo: state.minEmo = Emo5.None; break;
        }
        RemoveFromOrder(key);
    }

    void SetLevel4(HintKey key, Level4 v)
    {
        switch (key)
        {
            case HintKey.Move: state.move = v; break;
            case HintKey.Density: state.density = v; break;
            case HintKey.Order: state.order = v; break;
            case HintKey.Rhythm: state.rhythm = v; break;
            default:
                Debug.LogError($"SetLevel4 invalid key: {key}");
                break;
        }
    }

    void SetEmo5(HintKey key, Emo5 v)
    {
        switch (key)
        {
            case HintKey.MaxEmo: state.maxEmo = v; break;
            case HintKey.MinEmo: state.minEmo = v; break;
            default:
                Debug.LogError($"SetEmo5 invalid key: {key}");
                break;
        }
    }

    // ---- UI apply ----
    void ApplyAllToUI()
    {
        moveView.Apply(state.move);
        densityView.Apply(state.density);
        orderView.Apply(state.order);
        rhythmView.Apply(state.rhythm);

        maxEmoView.Apply(state.maxEmo);
        minEmoView.Apply(state.minEmo);
    }
}
