using System;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public const int PeriodTime = 25;

    public TimeRuntimeData Runtime { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsDeadlineExceeded => IsInitialized && Runtime.CurrentDate > Runtime.Deadline;

    public event Action<int, int> OnTimeChanged;
    public event Action<TimePeriod> OnPeriodChanged;
    public event Action<int> OnDateChanged;
    public event Action<TimePeriod> OnPeriodStarted;
    public event Action<int, int> OnDeadlineExceeded;

    private bool isEndingPeriod;
    private bool deadlineExceededNotified;

    private void Awake()
    {
        Runtime = new TimeRuntimeData();
    }

    public void Setup(int startDate, int deadline)
    {
        if (startDate < 1)
        {
            Debug.LogWarning($"{nameof(startDate)} must be at least 1. The value was changed to 1.");
            startDate = 1;
        }

        if (deadline < 1)
        {
            Debug.LogWarning($"{nameof(deadline)} must be at least 1. The value was changed to 1.");
            deadline = 1;
        }

        Runtime.CurrentDate = startDate;
        Runtime.Deadline = deadline;
        Runtime.Period = TimePeriod.Day;
        Runtime.MaxTime = PeriodTime;
        Runtime.CurrentTime = Runtime.MaxTime;

        IsInitialized = true;
        isEndingPeriod = false;
        deadlineExceededNotified = false;

        OnDateChanged?.Invoke(Runtime.CurrentDate);
        OnPeriodChanged?.Invoke(Runtime.Period);
        OnTimeChanged?.Invoke(Runtime.CurrentTime, Runtime.MaxTime);
        CheckDeadlineExceeded();
        OnPeriodStarted?.Invoke(Runtime.Period);
    }

    public void ConsumeTime(int amount)
    {
        if (!EnsureInitialized()) return;

        if (amount < 0)
        {
            Debug.LogWarning($"{nameof(amount)} cannot be negative.");
            return;
        }

        if (amount == 0 || isEndingPeriod) return;

        Runtime.CurrentTime = amount >= Runtime.CurrentTime
            ? 0
            : Runtime.CurrentTime - amount;

        OnTimeChanged?.Invoke(Runtime.CurrentTime, Runtime.MaxTime);

        if (Runtime.CurrentTime == 0)
        {
            EndPeriod();
        }
    }

    public void EndPeriod()
    {
        if (!EnsureInitialized() || isEndingPeriod) return;

        isEndingPeriod = true;

        try
        {
            if (Runtime.Period == TimePeriod.Day)
            {
                StartPeriod(TimePeriod.Night);
                return;
            }

            AdvanceDate();
            StartPeriod(TimePeriod.Day);
        }
        finally
        {
            isEndingPeriod = false;
        }
    }

    private void StartPeriod(TimePeriod period)
    {
        Runtime.Period = period;
        OnPeriodChanged?.Invoke(Runtime.Period);

        Runtime.MaxTime = PeriodTime;
        Runtime.CurrentTime = Runtime.MaxTime;
        OnTimeChanged?.Invoke(Runtime.CurrentTime, Runtime.MaxTime);

        OnPeriodStarted?.Invoke(Runtime.Period);
    }

    private void AdvanceDate()
    {
        Runtime.CurrentDate++;
        OnDateChanged?.Invoke(Runtime.CurrentDate);
        CheckDeadlineExceeded();
    }

    private void CheckDeadlineExceeded()
    {
        if (!IsDeadlineExceeded || deadlineExceededNotified) return;

        deadlineExceededNotified = true;
        OnDeadlineExceeded?.Invoke(Runtime.CurrentDate, Runtime.Deadline);
    }

    private bool EnsureInitialized()
    {
        if (IsInitialized) return true;

        Debug.LogError($"{nameof(TimeManager)} must be initialized with {nameof(Setup)} before use.");
        return false;
    }
}
