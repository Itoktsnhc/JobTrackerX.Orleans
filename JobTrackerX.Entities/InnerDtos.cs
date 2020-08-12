using System;
using JobTrackerX.SharedLibs;

namespace JobTrackerX.Entities
{
    public class ActionMessageDto
    {
        public long JobId { get; set; }
        public JobState JobState { get; set; }
        public ActionConfig ActionConfig { get; set; }
    }

    public class StateCheckMessageDto
    {
        public StateCheckConfig StateCheckConfig { get; set; }
        public long JobId { get; set; }
    }
    
    public class BufferDto
    {
        public BufferDto(Guid bufferId)
        {
            BufferId = bufferId;
        }

        public Guid BufferId { get; set; }
    }

    public class AddToBufferDto
    {
        public AddToBufferDto(long grainId, BufferedGrainInterfaceType grainType)
        {
            GrainIntId = grainId;
            GrainType = grainType;
        }

        public long GrainIntId { get; }
        public BufferedGrainInterfaceType GrainType { get; }

        public override int GetHashCode()
        {
            return HashCode.Combine(GrainIntId, GrainType);
        }

        public override bool Equals(object obj)
        {
            return GetHashCode() == obj?.GetHashCode();
        }
    }
    
    public static class SharedData
    {
        public static DateTimeOffset? LastMergeTimePoint { get; set; }
    }
}