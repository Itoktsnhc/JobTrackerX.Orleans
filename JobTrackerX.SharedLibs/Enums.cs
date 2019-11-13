namespace JobTrackerX.SharedLibs
{
    /// <summary>
    ///     任务状态：
    ///     0类： WaitingForActivation 初始状态，仅有该状态仅可被激活。
    ///     1类： WaitingToRun, Running, Waring->都为Pending状态，不存在状态变化
    ///     2类： RanToCompletion, Faulted, RanToCompletionWithWarning ->都为结束状态。
    ///     0 => 1 => 2: 如果出现状态类别回退，会被忽略。
    ///     任务的当前状态为时间序列上所有的状态汇总，遵循以下原则
    ///     1. 如果序列中有失败状态，则状态置为失败。
    ///     2. 否则如果状态中有成功则
    ///     Warning表示允许过程中出现部分错误，这些错误最终是否需要标记为失败需要看程序自身决定
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