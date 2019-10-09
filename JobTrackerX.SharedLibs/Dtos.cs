using System;
using System.Collections.Generic;

namespace JobTrackerX.SharedLibs
{
    public class AddJobDto
    {
        public AddJobDto()
        {
        }

        public AddJobDto(string jobName, long? parentJobId = null)
        {
            JobName = jobName;
            ParentJobId = parentJobId;
        }

        public string CreatedBy { get; set; }
        public string Options { get; set; }
        public List<string> Tags { get; set; }
        public long? ParentJobId { get; set; }
        public string JobName { get; set; }
    }

    public class UpdateJobStateDto
    {
        public UpdateJobStateDto(JobState jobState, string message = null)
        {
            JobState = jobState;
            Message = message;
        }

        public string Message { get; set; }
        public JobState JobState { get; set; }
    }

    public class StateChangeDto
    {
        public StateChangeDto()
        {
        }

        public StateChangeDto(JobState state, string additionMsg = null)
        {
            State = state;
            AdditionMsg = additionMsg;
            TimePoint = DateTimeOffset.Now;
        }

        public JobState State { get; set; }
        public string AdditionMsg { get; }
        public DateTimeOffset TimePoint { get; set; }
    }

    public class ReturnDto<TData>
    {
        public ReturnDto()
        {
        }

        public ReturnDto(TData data, bool result = true, string msg = "success")
        {
            Data = data;
            Result = result;
            Msg = msg;
        }

        public string Msg { get; set; } = "failed";
        public bool Result { get; set; }
        public TData Data { get; set; }
    }

    public class QueryJobIndexDto
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string Predicate { get; set; }
    }

    public class ReturnQueryIndexDto
    {
        public List<JobIndex> Indices { get; set; }
        public int IndexGrainHit { get; set; }
        public int TotalCount { get; set; }
    }

    //public class QueryCondition
    //{
    //    public QueryCondition(string value, bool useRegex = false)
    //    {
    //        UseRegex = useRegex;
    //        Value = value;
    //    }

    //    public bool UseRegex { get; set; }
    //    public string Value { get; set; }
    //}

    public class UpdateJobOptionsDto
    {
        public UpdateJobOptionsDto(string options)
        {
            Options = options;
        }

        public string Options { get; set; }
    }

    public class UpdateIdOffsetDto
    {
        public UpdateIdOffsetDto(long offset)
        {
            Offset = offset;
        }

        public long Offset { get; set; }
    }
}