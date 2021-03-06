﻿using System;
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

        public long? JobId { get; set; }
        public string CreatedBy { get; set; }
        public string Options { get; set; }
        public List<string> Tags { get; set; }
        public long? ParentJobId { get; set; }
        public string JobName { get; set; }
        public string SourceLink { get; set; }
        public List<ActionConfig> ActionConfigs { get; set; }
        public List<StateCheckConfig> StateCheckConfigs { get; set; }
        public bool TrackJobCount { get; set; }
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
        public string AdditionMsg { get; set; }
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

    public class HttpActionBody
    {
        public object Payload { get; set; }
        public HttpActionConfig Config { get; set; }
        public long JobId { get; set; }
        public JobState JobState { get; set; }
    }

    public class StateCheckActionBody : HttpActionBody
    {
        public List<JobState> TargetJobStateList { get; set; }
    }

    public class AppendLogDto
    {
        public AppendLogDto(string content)
        {
            Content = content;
        }
        public string Content { get; set; }
    }
    
    public class BufferedContent
    {
        public long GrainIntId { get; set; }
        public BufferedGrainInterfaceType GrainType { get; set; }
    }
    
    public class AddJobErrorResult
    {
        public long JobId { get; set; }
        public string Error { get; set; }
    }
    
    public class BatchAddJobDto
    {
        public long ParentJobId { get; set; }
        public List<AddJobDto> Children { get; set; }
    }
}