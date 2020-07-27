namespace JobTrackerX.SharedLibs
{
    /// <summary>
    ///     任务状态：
    ///     0类： WaitingForActivation 初始状态，仅有该状态仅可被激活。
    ///     1类： WaitingToRun, Running, Warning
    ///     2类： RanToCompletion, Faulted ->都为结束状态。
    /// </summary>
    public enum JobState
    {
        WaitingForActivation = 0,
        WaitingToRun = 1,
        Faulted = 2,
        RanToCompletion = 3,
        Running = 4,
        WaitingForChildrenToComplete = 5,
        Warning = 7
    }

    public enum JobStateCategory
    {
        None = 0,
        Pending = 1,
        Successful = 2,
        Failed = 3
    }

    public enum TimeIndexType
    {
        ByHour = 0,
        ByDay = 1
    }
}