using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.SharedLibs
{
    public class JobTrackerBatchOperation
    {
        public readonly List<Func<Task>> CachedFuncs;
        public readonly ExecutionDataflowBlockOptions Options;

        public JobTrackerBatchOperation(ExecutionDataflowBlockOptions options = null)
        {
            Options = options ?? new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            CachedFuncs = new List<Func<Task>>();
        }

        public void Add(Func<Task> action)
        {
            CachedFuncs.Add(action);
        }
    }
}