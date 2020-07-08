using System;

namespace JobTrackerX.WebApi.Entities
{
    public static class SharedData
    {
        public static DateTimeOffset? LastMergeTimePoint { get; set; }
    }
}
