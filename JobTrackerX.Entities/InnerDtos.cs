using JobTrackerX.SharedLibs;
using System.Collections.Generic;

namespace JobTrackerX.Entities
{
    public class AddJobDtoInner
    {
        public AddJobDtoInner(AddJobDto dto)
        {
            JobName = dto.JobName;
            ParentJobId = dto.ParentJobId;
            Tags = dto.Tags;
            CreatedBy = dto.CreatedBy;
            Options = dto.Options;
        }

        public AddJobDtoInner()
        {
        }

        public string CreatedBy { get; set; }
        public string Options { get; set; }
        public List<string> Tags { get; set; }
        public long? ParentJobId { get; set; }
        public string JobName { get; set; }
    }

    public class UpdateJobStateDtoInner
    {
        public UpdateJobStateDtoInner()
        {
        }

        public UpdateJobStateDtoInner(UpdateJobStateDto dto)
        {
            AdditionMsg = dto.Message;
            JobState = dto.JobState;
        }

        public string AdditionMsg { get; set; }
        public JobState JobState { get; set; }
    }

    public class GetNewIdsDto
    {
        public GetNewIdsDto(int count, long offset)
        {
            Count = count;
            Offset = offset;
        }

        public int Count { get; set; }
        public long Offset { get; set; }
    }
}