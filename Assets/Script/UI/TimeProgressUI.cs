using UnityEngine;

[DisallowMultipleComponent]
public class TimeProgressUI : MonoBehaviour
{
    [SerializeField] private RectTransform timeBar;
    [SerializeField] private RectTransform clockHand;

    private TimeManager timeManager;
    private bool isSubscribed;

    private void OnEnable()
    {
        TrySubscribe();
        RefreshFromCurrentTime();
    }

    private void Start()
    {
        // OnEnable can run before GameManager has finished Awake in some scene layouts.
        TrySubscribe();
        RefreshFromCurrentTime();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;

        RefreshFromCurrentTime();
    }

    private void TrySubscribe()
    {
        TimeManager resolvedTimeManager = GameManager.Instance?.TimeManager;
        if (resolvedTimeManager == null || (isSubscribed && timeManager == resolvedTimeManager)) return;

        Unsubscribe();

        timeManager = resolvedTimeManager;
        timeManager.OnTimeChanged += HandleTimeChanged;
        timeManager.OnPeriodStarted += HandlePeriodStarted;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (isSubscribed && timeManager != null)
        {
            timeManager.OnTimeChanged -= HandleTimeChanged;
            timeManager.OnPeriodStarted -= HandlePeriodStarted;
        }

        isSubscribed = false;
        timeManager = null;
    }

    private void HandleTimeChanged(int currentTime, int maxTime)
    {
        RefreshPosition(currentTime, maxTime);
    }

    private void HandlePeriodStarted(TimePeriod period)
    {
        RefreshFromCurrentTime();
    }

    private void RefreshFromCurrentTime()
    {
        if (timeManager == null || !timeManager.IsInitialized)
        {
            SetProgress(0f);
            return;
        }

        RefreshPosition(
            timeManager.Runtime.CurrentTime,
            timeManager.Runtime.MaxTime);
    }

    private void RefreshPosition(int currentTime, int maxTime)
    {
        float progress = maxTime > 0
            ? 1f - (float)Mathf.Clamp(currentTime, 0, maxTime) / maxTime
            : 0f;

        SetProgress(progress);
    }

    private void SetProgress(float progress)
    {
        if (timeBar == null || clockHand == null) return;

        float handX = Mathf.Lerp(
            timeBar.rect.xMin,
            timeBar.rect.xMax,
            Mathf.Clamp01(progress));

        Vector3 handPosition = clockHand.localPosition;
        handPosition.x = handX;
        clockHand.localPosition = handPosition;
    }
}
