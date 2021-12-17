using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.SampleTrigger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var domain = Environment.GetEnvironmentVariable("JOBSYS");
            var client = new JobTrackerClient(domain);
            while (true)
            {
                Console.WriteLine("Cycle Start");
                await CreateSuccessJobAsync(client);
                await CreateErrorJobAsync(client);
                await Task.Delay(TimeSpan.FromMinutes(30));
            }
        }

        private static async Task CreateErrorJobAsync(IJobTrackerClient client)
        {
            var root = await client.CreateNewJobAsync(new AddJobDto("errorJob"));
            var layer1Child1 = await client.CreateNewJobAsync(new AddJobDto("", root.JobId));
            var layer2Child1 = await client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer2Child2 = await client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer2Child3 = await client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer1Child2 = await client.CreateNewJobAsync(new AddJobDto("", root.JobId));
            await client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await client.UpdateJobStatesAsync(layer1Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await client.UpdateJobStatesAsync(layer1Child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await client.UpdateJobStatesAsync(layer2Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            await client.UpdateJobStatesAsync(layer2Child2.JobId, new UpdateJobStateDto(JobState.Faulted));
            await client.UpdateJobStatesAsync(layer2Child3.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
        }



        private static async Task CreateSuccessJobAsync(IJobTrackerClient client)
        {
            var root = await client.CreateNewJobAsync(new AddJobDto($"ROOT JOB")
            {
                Tags = new List<string>() { "演示用" },
                CreatedBy = "演示创建人",
                SourceLink = "https://blog.itok.xyz/"
            });
            await client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            var child1 = await client.CreateNewJobAsync(new AddJobDto($"L1-JOB-1", root.JobId));

            var child2 = await client.CreateNewJobAsync(new AddJobDto($"L1-JOB-2", root.JobId));
            await client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.Running));
            await client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            await client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            await client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Running));
            var child1Child1 = await client.CreateNewJobAsync(new AddJobDto($"L1-JOB-1/L2-JOB-1", child1.JobId)
            {
                Tags = new List<string>() { "演示用" },
                CreatedBy = "演示创建人",
            });
            await client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await client.UpdateJobStatesAsync(child1Child1.JobId, new UpdateJobStateDto(JobState.Running));
            await client.UpdateJobStatesAsync(child1Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
        }
    }

    public static class Extension
    {
        public static async Task PostToBlockUntilSuccessAsync<TInput>(this ITargetBlock<TInput> block, TInput input)
        {
            while (!block.Post(input))
            {
                await Task.Delay(10);
            }
        }

        public static ExecutionDataflowBlockOptions GetOutOfGrainExecutionOptions()
        {
            return new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 100
            };
        }
    }
}