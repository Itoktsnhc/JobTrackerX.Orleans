using JobTrackerX.Client;
using JobTrackerX.Entities;
using JobTrackerX.SharedLibs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Test
{
    [TestClass]
    public class JobTrackerClientTest
    {
        private readonly IJobTrackerClient _client;

        public JobTrackerClientTest()
        {
            _client = new JobTrackerClient("http://jobsystemtestvm.chinaeast2.cloudapp.chinacloudapi.cn");
        }

        [TestMethod]
        public async Task TestChildrenTrigger()
        {
            var rootJob = await _client.CreateNewJobAsync(new AddJobDto { JobName = "RootJob" });
            Console.WriteLine($"RootJobId {rootJob.JobId}");
            await _client.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.Running, "rootJobRunning"));
            var child1 = await _client.CreateNewJobAsync(new AddJobDto("child1", rootJob.JobId));
            var child2 = await _client.CreateNewJobAsync(new AddJobDto("child2", rootJob.JobId));
            await _client.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            await _client.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.Running, "rootJobRunningAgain"));
            await _client.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            rootJob = await _client.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, rootJob.CurrentJobState);

            await _client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Warning, "child1 Running"));
            await _client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.Running, "child2 Running"));

            await _client.UpdateJobStatesAsync(child1.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "child1 finished"));
            await _client.UpdateJobStatesAsync(child2.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "child2 finished"));

            child1 = await _client.GetJobEntityAsync(child1.JobId);
            child2 = await _client.GetJobEntityAsync(child2.JobId);
            rootJob = await _client.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual(JobState.RanToCompletion, rootJob.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child1.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child2.CurrentJobState);
        }

        [TestMethod]
        public async Task TestMultiLayer()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("rootJob"));
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
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            await _client.UpdateJobStatesAsync(layer2Child2.JobId, new UpdateJobStateDto(JobState.Faulted));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            await _client.UpdateJobStatesAsync(layer2Child3.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
        }

        [TestMethod]
        public async Task TestManyTasks()
        {
            var createChildrenBlock = new TransformBlock<JobEntity, List<JobEntity>>(async entity =>
            {
                var res = new List<JobEntity>();
                for (var i = 0; i < 100; i++)
                {
                    res.Add(await _client.CreateNewJobAsync(new AddJobDto("ABC", entity.JobId)));
                }

                return res;
            });
            var adapter = new TransformManyBlock<List<JobEntity>, JobEntity>(entities => entities);
            var updateStateBlock = new ActionBlock<JobEntity>(async entity =>
            {
                await _client.UpdateJobStatesAsync(entity.JobId,
                    new UpdateJobStateDto(entity.JobId % 2 == 0 ? JobState.RanToCompletion : JobState.Faulted));
            }, Helper.GetOutOfGrainExecutionOptions());
            var root = await _client.CreateNewJobAsync(new AddJobDto("rootJob"));
            Console.WriteLine($"RootJobId {root.JobId}");
            createChildrenBlock.LinkTo(adapter, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
            adapter.LinkTo(updateStateBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
            await createChildrenBlock.PostToBlockUntilSuccessAsync(root);
            createChildrenBlock.Complete();
            await updateStateBlock.Completion;
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
        }

        [TestMethod]
        public async Task TestOptionUpdate()
        {
            var jobName = "foobar";
            var options = "balabala";
            var rootJob = await _client.CreateNewJobAsync(new AddJobDto(jobName)
            {
                Options = options
            });
            Assert.AreEqual(options, rootJob.Options);
            await _client.UpdateJobOptionsAsync(rootJob.JobId, new UpdateJobOptionsDto("Hello World"));
            rootJob = await _client.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual("Hello World", rootJob.Options);
        }

        [TestMethod]
        public async Task TestQueryJobs()
        {
            var jobName = "foobar" + DateTime.Now.Ticks;
            var createdBy = "itok";
            var tags = new[] { "red", "rootJob", "named" };
            await _client.CreateNewJobAsync(new AddJobDto(jobName)
            {
                CreatedBy = createdBy,
                Tags = tags.ToList()
            });
            var indices = await _client.QueryJobIndexAsync(new QueryJobIndexDto
            {
                Start = DateTimeOffset.Now.AddHours(-1),
                End = DateTimeOffset.Now.AddHours(1),
                Predicate = $@"JobName=""{jobName}"" and CreatedBy=""{createdBy}"" and Tags.Contains(""red"")"
            });
            Assert.AreEqual(indices.IndexGrainHit > 0, true);
        }
    }
}