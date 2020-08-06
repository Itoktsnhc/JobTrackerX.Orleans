using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using JobTrackerX.Client;
using JobTrackerX.Entities;
using JobTrackerX.SharedLibs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JobTrackerX.Test
{
    [TestClass]
    public class TestsWithBuffer
    {
        private readonly IJobTrackerClient _rawClient;

        public TestsWithBuffer()
        {
            var handler = new HttpClientHandler()
            {
                Proxy = null,
                UseProxy = false
            };
            var httpClient = new HttpClient(handler)
            {
                BaseAddress =
                    new Uri("http://localhost:45001/", UriKind.Absolute)
            };

            _rawClient = new JobTrackerClient(httpClient,
                AddTokenHeader, 3, _ => TimeSpan.FromSeconds(3));
        }

        private Task AddTokenHeader(HttpRequestMessage req)
        {
            req.Headers.TryAddWithoutValidation("x-jobtracker-token", "E37D3D5D924E4F739BE3774B44D49EF3");
            return Task.CompletedTask;
        }

        [TestMethod]
        public void TestGetHashCode()
        {
            var buffer = new HashSet<AddToBufferDto>(1, new AddToBufferDtoEqualityComparer());
            foreach (var _ in Enumerable.Range(1, 10))
            {
                buffer.Add(new AddToBufferDto(1044003, BufferedGrainInterfaceType.JobGrain));
            }

            Assert.AreEqual(buffer.Count, 1);
            buffer.Add(null);
            buffer.Add(null);
            buffer.Add(null);
            Assert.AreEqual(buffer.Count, 2);
        }

        [TestMethod]
        public async Task TestJobPersistAsync()
        {
            var context = new JobTrackerContext(_rawClient);
            var jobName = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            var root = await context.CreateNewJobAsync(new AddJobDto(jobName));
            await context.CommitAndCloseAsync();
            root = await _rawClient.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(jobName, root.JobName);
        }

        [TestMethod]
        public async Task TestMutiLayerWithBuffer()
        {
            var context = new JobTrackerContext(_rawClient);
            var root = await context.CreateNewJobAsync(new AddJobDto("TestMultiLayer"));
            var layer1Child1 = await context.CreateNewJobAsync(new AddJobDto("", root.JobId));
            var layer2Child1 = await context.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer2Child2 = await context.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer2Child3 = await context.CreateNewJobAsync(new AddJobDto("", layer1Child1.JobId));
            var layer1Child2 = await context.CreateNewJobAsync(new AddJobDto("", root.JobId));
            Console.WriteLine($"RootJobId {root.JobId}");

            await context.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await context.UpdateJobStatesAsync(layer1Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await context.UpdateJobStatesAsync(layer1Child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await context.UpdateJobStatesAsync(layer2Child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await context.CommitAndCloseAsync();

            root = await _rawClient.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            await _rawClient.UpdateJobStatesAsync(layer2Child2.JobId, new UpdateJobStateDto(JobState.Faulted));
            root = await _rawClient.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            await _rawClient.UpdateJobStatesAsync(layer2Child3.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await _rawClient.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
        }

        [TestMethod]
        public async Task TestChildrenTrigger()
        {
            var context = new JobTrackerContext(_rawClient);
            var rootJob = await context.CreateNewJobAsync(new AddJobDto {JobName = "TestChildrenTrigger"});
            Console.WriteLine($"RootJobId {rootJob.JobId}");
            await context.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.Running, "rootJobRunning"));
            var child1 = await context.CreateNewJobAsync(new AddJobDto("child1", rootJob.JobId));
            var child2 = await context.CreateNewJobAsync(new AddJobDto("child2", rootJob.JobId));
            await context.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            await context.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.Running, "rootJobRunningAgain"));
            await context.UpdateJobStatesAsync(rootJob.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            rootJob = await context.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, rootJob.CurrentJobState);

            await context.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Warning, "child1 Running"));
            await context.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Running, "child1 Running"));
            await Task.Delay(500);
            await context.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.Running, "child2 Running"));
            await Task.Delay(500);

            await context.UpdateJobStatesAsync(child1.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "child1 finished"));
            await context.UpdateJobStatesAsync(child2.JobId,
                new UpdateJobStateDto(JobState.RanToCompletion, "child2 finished"));

            child1 = await context.GetJobEntityAsync(child1.JobId);
            child2 = await context.GetJobEntityAsync(child2.JobId);
            rootJob = await context.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual(JobState.RanToCompletion, rootJob.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child1.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child2.CurrentJobState);
            await context.CommitAndCloseAsync();

            child1 = await _rawClient.GetJobEntityAsync(child1.JobId);
            child2 = await _rawClient.GetJobEntityAsync(child2.JobId);
            rootJob = await _rawClient.GetJobEntityAsync(rootJob.JobId);
            Assert.AreEqual(JobState.RanToCompletion, rootJob.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child1.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child2.CurrentJobState);
        }

        [TestMethod]
        public async Task TestReverseAsync()
        {
            var context = new JobTrackerContext(_rawClient);
            var root = await context.CreateNewJobAsync(new AddJobDto("TestReverseAsync"));
            var child = await context.CreateNewJobAsync(new AddJobDto("", root.JobId));
            var childChild = await context.CreateNewJobAsync(new AddJobDto("", child.JobId));

            await context.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await context.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);

            await context.UpdateJobStatesAsync(child.JobId, new UpdateJobStateDto(JobState.Faulted));
            child = await context.GetJobEntityAsync(child.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, child.CurrentJobState);

            await context.UpdateJobStatesAsync(childChild.JobId,
                new UpdateJobStateDto(JobState.WaitingForChildrenToComplete));
            childChild = await context.GetJobEntityAsync(childChild.JobId);
            Assert.AreEqual(JobState.RanToCompletion, childChild.CurrentJobState);

            child = await context.GetJobEntityAsync(child.JobId);
            Assert.AreEqual(JobState.Faulted, child.CurrentJobState);

            root = await context.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
        }
    }
}