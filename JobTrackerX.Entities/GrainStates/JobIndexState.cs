using Microsoft.Azure.Cosmos.Table;
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
        public Dictionary<long, JobIndexInternal> JobIndices { get; set; } = new Dictionary<long, JobIndexInternal>();
    }

    public class CompressIndexWrapper
    {
        public byte[] DataArray { get; set; }
    }

    public class JobIndexInternal : ITableEntity
    {
        public JobIndexInternal()
        {
        }

        public JobIndexInternal(long jobId, string jobName, string createdBy, List<string> tags)
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

        public DateTimeOffset? IndexTime { get; set; } = DateTimeOffset.Now;
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string ETag { get; set; }

        public void ReadEntity(IDictionary<string, EntityProperty> properties,
            OperationContext operationContext)
        {
            TableEntity.ReadUserObject(this, properties, operationContext);
            if (properties.ContainsKey(nameof(Tags)))
            {
                Tags = JsonConvert.DeserializeObject<List<string>>(properties[nameof(Tags)].StringValue);
            }
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var s = TableEntity.WriteUserObject(this, operationContext);
            s[nameof(Tags)] = new EntityProperty(JsonConvert.SerializeObject(Tags));
            return s;
        }
    }
}