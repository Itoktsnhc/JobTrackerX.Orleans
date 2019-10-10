using JobTrackerX.SharedLibs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JobTrackerX.Entities.GrainStates
{
    public class JobEntityState
    {
        public long JobId { get; set; }
        public string JobName { get; set; }
        public long? ParentJobId { get; set; }
        public long AncestorJobId { get; set; }
        public List<string> Tags { get; set; }
        public string Options { get; set; }
        public string CreateBy { get; set; }

        public List<StateChangeDto> StateChanges { get; } = new List<StateChangeDto>();

        public JobState CurrentJobState
        {
            get
            {
                if (StateChanges == null || StateChanges.Count == 0)
                {
                    return JobState.WaitingForActivation;
                }

                var returnState = StateChanges.Last().State;
                if (Helper.FinishedOrWaitingForChildrenOrFaultedJobStates.Contains(returnState))
                {
                    if (SuccessChildrenCount == TotalChildrenCount)
                    {
                        returnState = StateChanges.Any(s => s.State == JobState.Faulted)
                            ? JobState.Faulted
                            : JobState.RanToCompletion;
                    }

                    if (PendingChildrenCount == 0 && FailedChildrenCount > 0)
                    {
                        returnState = JobState.Faulted;
                    }

                    if (PendingChildrenCount > 0)
                    {
                        returnState = JobState.WaitingForChildrenToComplete;
                    }
                }

                return returnState;
            }
        }

        public DateTimeOffset? StartTime
        {
            get { return StateChanges.Find(s => s.State == JobState.Running)?.TimePoint; }
        }

        public DateTimeOffset? EndTime
        {
            get
            {
                return StateChanges.LastOrDefault(s =>
                    Helper.FinishedJobStates
                        .Contains(s.State))?.TimePoint;
            }
        }

        public DateTimeOffset? CreateTime
        {
            get { return StateChanges.Find(s => s.State == JobState.WaitingToRun)?.TimePoint; }
        }

        public int TotalChildrenCount => ChildrenStatesDic.Count;

        public int SuccessChildrenCount
        {
            get
            {
                return ChildrenStatesDic.Count(s =>
                    s.Value == JobStateCategory.Success);
            }
        }

        public int FailedChildrenCount
        {
            get
            {
                return ChildrenStatesDic.Count(s =>
                    s.Value == JobStateCategory.Failed);
            }
        }

        public int PendingChildrenCount
        {
            get
            {
                return ChildrenStatesDic.Count(s =>
                    s.Value == JobStateCategory.Pending);
            }
        }

        public Dictionary<long, JobStateCategory> ChildrenStatesDic { get; } = new Dictionary<long, JobStateCategory>();
    }
}