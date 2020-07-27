using System;

namespace JobTrackerX.Entities.GrainStates
{
    public class JobTreeStatisticsState
    {
        public long JobId { get; set; }
        public JobTreeStateItemInternal TreeStart { get; set; }
        public JobTreeStateItemInternal TreeEnd { get; set; }
        public TimeSpan? ExecutionTime
        {
            get
            {
                return TreeEnd?.TimePoint - TreeStart?.TimePoint;
            }
        }
    }

    public class JobTreeStateItemInternal
    {
        public JobTreeStateItemInternal(long sourceJobId)
        {
            SourceJobId = sourceJobId;
            TimePoint = DateTimeOffset.Now;
        }
        public long SourceJobId { get; set; }
        public DateTimeOffset TimePoint { get; set; }
    }
}