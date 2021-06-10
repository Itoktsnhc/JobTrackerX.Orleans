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
                var root = await _client.CreateNewJobAsync(new AddJobDto($"ROOT JOB")
                {
                    Tags = new List<string>() { "演示用" },
                    CreatedBy = "演示创建人",
                    SourceLink = "https://blog.itok.xyz/"
                });
                await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
                var child1 = await _client.CreateNewJobAsync(new AddJobDto($"L1-JOB-1", root.JobId));

                var child2 = await _client.CreateNewJobAsync(new AddJobDto($"L1-JOB-2", root.JobId));
                await _client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.Running));
                await _client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

                await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

                await _client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Running));
                var child1Child1 = await _client.CreateNewJobAsync(new AddJobDto($"L1-JOB-1/L2-JOB-1", child1.JobId)
                {
                    Tags = new List<string>() { "演示用" },
                    CreatedBy = "演示创建人",
                });
                await _client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                await _client.UpdateJobStatesAsync(child1Child1.JobId, new UpdateJobStateDto(JobState.Running));
                await _client.UpdateJobStatesAsync(child1Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
                await Task.Delay(TimeSpan.FromMinutes(7));
            }
        }
    }
}
