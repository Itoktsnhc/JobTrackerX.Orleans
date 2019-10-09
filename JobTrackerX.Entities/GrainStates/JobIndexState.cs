using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace JobTrackerX.Entities.GrainStates
{
    public class AggregateJobIndexState
    {
        public int RollingIndexCount { get; set; }
    }

    public class JobIndexState
    {
        public Dictionary<long, JobIndexInner> JobIndices { get; set; } = new Dictionary<long, JobIndexInner>();
    }

    public class CompressIndexWrapper
    {
        public byte[] DataArray { get; set; }
    }

    public class JobIndexInner : TableEntity
    {
        public JobIndexInner()
        {
        }

        public JobIndexInner(long jobId, string jobName, string createdBy, List<string> tags)
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

        public DateTimeOffset IndexTime { get; set; } = DateTimeOffset.Now;

        public override void ReadEntity(IDictionary<string, EntityProperty> properties,
            OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            if (properties.ContainsKey(nameof(Tags)))
            {
                Tags = JsonConvert.DeserializeObject<List<string>>(properties[nameof(Tags)].StringValue);
            }
        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var s = base.WriteEntity(operationContext);
            s[nameof(Tags)] = new EntityProperty(JsonConvert.SerializeObject(Tags));
            return s;
        }
    }
}