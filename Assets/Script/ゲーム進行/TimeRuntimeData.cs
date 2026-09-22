using System;

public enum TimePeriod
{
    Day,
    Night
}

[Serializable]
public class TimeRuntimeData
{
    public int CurrentDate;
    public int Deadline;
    public TimePeriod Period;
    public int CurrentTime;
    public int MaxTime;
}
