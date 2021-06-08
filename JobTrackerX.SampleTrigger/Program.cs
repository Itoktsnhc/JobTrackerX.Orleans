using JobTrackerX.Client;
using JobTrackerX.SharedLibs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.SampleTrigger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var domain = Environment.GetEnvironmentVariable("JOBSYS");
            var _client = new JobTrackerClient(domain);
            while (true)
            {
                Console.WriteLine("Cycle Start");
                var root = await _client.CreateNewJobAsync(new AddJobDto($"JOB-{DateTimeOffset.UtcNow:yy-MM-dd HH:mm}")
                {
                    Tags = new List<string>() { "示例任务" },
                    CreatedBy = "SampleTrigger",
                    SourceLink = "这是任务来源链接"
                });
                var layer1Child1 = await _client.CreateNewJobAsync(new AddJobDto("", root.JobId));
                var layer2Child1 = await _client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
                var layer2Child2 = await _client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
                var layer2Child3 = await _client.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
                var layer1Child2 = await _client.CreateNewJobAsync(new AddJobDto("", root.JobId));
                Console.WriteLine($"RootJobId {root.JobId}");

                await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                await _client.UpdateJobStatesAsync(layer1Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                await _client.UpdateJobStatesAsync(layer1Child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                await _client.UpdateJobStatesAsync(layer2Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

                root = await _client.GetJobEntityAsync(root.JobId);
                Console.WriteLine($"root job cur status : {root.JobId} {root.CurrentJobState}");
                await _client.UpdateJobStatesAsync(layer2Child2.JobId, new UpdateJobStateDto(JobState.Faulted));
                root = await _client.GetJobEntityAsync(root.JobId); ;
                Console.WriteLine($"root job cur status : {root.JobId} {root.CurrentJobState}");
                await _client.UpdateJobStatesAsync(layer2Child3.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                root = await _client.GetJobEntityAsync(root.JobId);
                Console.WriteLine($"root job cur status : {root.JobId} {root.CurrentJobState}");
                await Task.Delay(TimeSpan.FromMinutes(6));
            }
        }
    }
}
