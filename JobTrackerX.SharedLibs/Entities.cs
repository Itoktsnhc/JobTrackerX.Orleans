using System;
using System.Collections.Generic;

namespace JobTrackerX.SharedLibs
{
    public class JobEntity
    {
        public long JobId { get; set; }
        public string JobName { get; set; }
        public long? ParentJobId { get; set; }
        public long? AncestorJobId { get; set; }
        public List<string> Tags { get; set; }
        public string Options { get; set; }
        public string CreatedBy { get; set; }
        public List<StateChangeDto> StateChanges { get; set; }
        public JobState CurrentJobState { get; set; }
        public DateTimeOffset? CreateTime { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public int TotalChildrenCount { get; set; }
        public int SuccessfulChildrenCount { get; set; }
        public int FailedChildrenCount { get; set; }
        public int PendingChildrenCount { get; set; }
        public Dictionary<long, JobStateCategory> ChildrenStatesDic { get; set; }
        public string SourceLink { get; set; }
        public List<ActionConfig> ActionConfigs { get; set; }
    }

    public class JobIndex
    {
        public JobIndex()
        {
        }

        public JobIndex(long jobId, string jobName, string createdBy, List<string> tags)
        {
            JobId = jobId;
            JobName = jobName;
            CreatedBy = createdBy;
            Tags = tags ?? new List<string>();
        }

        public long JobId { get; set; }
        public string JobName { get; set; }
        public string CreatedBy { get; set; }
        public List<string> Tags { get; set; }
        public DateTimeOffset IndexTime { get; set; }
    }

    public class ActionConfig
    {
        public List<JobState> JobStateFilters { get; set; }
        public ActionConfigWrapper ActionWrapper { get; set; }
    }

    public class EmailActionConfig
    {
        public IList<string> Recipients { get; set; }
    }

    public class HttpActionConfig
    {
        public string Url { get; set; }
        public object Payload { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }

    public class ActionConfigWrapper
    {
        public EmailActionConfig EmailConfig { get; set; }
        public HttpActionConfig HttpConfig { get; set; }
    }
}