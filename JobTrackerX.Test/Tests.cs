using JobTrackerX.Client;
using JobTrackerX.Entities;
using JobTrackerX.SharedLibs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Test
{
    [TestClass]
    public class Tests
    {
        private const string BaseUrlStr = "http://localhost:45001/";
        private readonly IJobTrackerClient _client;

        public Tests()
        {
            _client = new JobTrackerClient(new HttpClient()
            {
                BaseAddress = new Uri(BaseUrlStr)
            },
            retryCount: 3, retryInterval: _ => TimeSpan.FromSeconds(3));
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
            const string jobName = "foobar";
            const string options = "balabala";
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
        public async Task TestReverseAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto());
            var child = await _client.CreateNewJobAsync(new AddJobDto("", root.JobId));
            var childchild = await _client.CreateNewJobAsync(new AddJobDto("", child.JobId));

            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);

            await _client.UpdateJobStatesAsync(child.JobId, new UpdateJobStateDto(JobState.Faulted));
            child = await _client.GetJobEntityAsync(child.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, child.CurrentJobState);

            await _client.UpdateJobStatesAsync(childchild.JobId, new UpdateJobStateDto(JobState.WaitingForChildrenToComplete));
            childchild = await _client.GetJobEntityAsync(childchild.JobId);
            Assert.AreEqual(JobState.RanToCompletion, childchild.CurrentJobState);

            child = await _client.GetJobEntityAsync(child.JobId);
            Assert.AreEqual(JobState.Faulted, child.CurrentJobState);

            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
        }

        [TestMethod]
        public async Task TestEmailActionTriggerAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("testTrigger")
            {
                ActionConfigs = new List<ActionConfig>()
                {
                    new ActionConfig()
                    {
                        JobStateFilters = new List<JobState>(){ JobState.RanToCompletion, JobState.Running},
                        ActionWrapper= new ActionConfigWrapper(){
                        EmailConfig=new EmailActionConfig(){
                        Recipients=new List<string>() } } /*"xx@xxx.com"*/
                    }
                }
            });
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
        }

        [TestMethod]
        public async Task TestPostActionTriggerAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("testTrigger")
            {
                ActionConfigs = new List<ActionConfig>()
                {
                    new ActionConfig()
                    {
                        JobStateFilters = new List<JobState>(){ JobState.RanToCompletion, JobState.Running,JobState.Faulted},
                        ActionWrapper= new ActionConfigWrapper()
                        {
                            HttpConfig=new HttpActionConfig()
                            {
                                Headers=new Dictionary<string, string>(){ { "x-test", "val" } },
                                Payload=new { Name="this isName",Value="this is vv"},
                                Url = "http://www.azure.com/test"
                            }
                        }
                    }
                }
            });
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
        }

        [TestMethod]
        public async Task TestJobLogAsync()
        {
            var job = await _client.CreateNewJobAsync(new AddJobDto("balabla"));
            await _client.AppendToJobLogAsync(job.JobId, new AppendLogDto("123456"));
            await _client.AppendToJobLogAsync(job.JobId, new AppendLogDto("123456"));
            await _client.AppendToJobLogAsync(job.JobId, new AppendLogDto("123456"));
            await _client.AppendToJobLogAsync(job.JobId, new AppendLogDto("123456"));
        }

        [TestMethod]
        public async Task TestStateCheckAsync()
        {
            await _client.CreateNewJobAsync(new AddJobDto()
            {
                JobName = nameof(TestStateCheckAsync),
                StateCheckConfigs = new List<StateCheckConfig>()
                {
                    new StateCheckConfig()
                    {
                        CheckTime =DateTimeOffset.Now.AddSeconds(30),
                        FailedAction = new ActionConfigWrapper()
                        {
                            EmailConfig = new EmailActionConfig()
                            {
                                Recipients = new List<string>(){ "foo@bar.com"}
                            }
                        },
                        TargetStateList = new List<JobState>()
                        {
                            JobState.Faulted,
                            JobState.RanToCompletion
                        }
                    }
                }
            });
        }

        [TestMethod]
        public async Task TestEndTimeDelayAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("TestEndTimeDelayAsync")
            {
                SourceLink = "https://www.cnblogs.com/"
            });
            var child1 = await _client.CreateNewJobAsync(new AddJobDto("TestEndTimeDelayAsync'child1", root.JobId));
            var child1Child = await _client.CreateNewJobAsync(new AddJobDto("TestEndTimeDelayAsync'child1'child", child1.JobId));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Running));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            await _client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Faulted));
            root = await _client.GetJobEntityAsync(root.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, root.CurrentJobState);
            child1 = await _client.GetJobEntityAsync(child1.JobId);
            Assert.AreEqual(JobState.WaitingForChildrenToComplete, child1.CurrentJobState);
            root = await _client.GetJobEntityAsync(root.JobId);
            var ts = TimeSpan.FromSeconds(5);
            await Task.Delay(ts);
            await _client.UpdateJobStatesAsync(child1Child.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(child1Child.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            root = await _client.GetJobEntityAsync(root.JobId);
            child1 = await _client.GetJobEntityAsync(child1.JobId);
            child1Child = await _client.GetJobEntityAsync(child1Child.JobId);
            Assert.AreEqual(JobState.Faulted, root.CurrentJobState);
            Assert.AreEqual(JobState.Faulted, child1.CurrentJobState);
            Assert.AreEqual(JobState.RanToCompletion, child1Child.CurrentJobState);
            var tree = await _client.GetJobTreeStatisticsAsync(root.JobId);
            var span = tree.ExecutionTime;
            Assert.IsNotNull(span);
            Assert.AreEqual(true, span >= ts);
        }

        [TestMethod]
        public async Task TestJobStatisticsAsync1()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("JobStatisticsTestRoot1"));
            var sub1 = await _client.CreateNewJobAsync(new AddJobDto("child1", root.JobId));
            var sub2 = await _client.CreateNewJobAsync(new AddJobDto("child2", root.JobId));
            var subsub1 = await _client.CreateNewJobAsync(new AddJobDto("child1child1", sub1.JobId));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            var rootS = await _client.GetJobTreeStatisticsAsync(root.JobId);
            Assert.IsNotNull(rootS.ExecutionTime);
            Assert.IsNotNull(rootS.TreeEnd);
            Assert.IsNotNull(rootS.TreeStart);
            Assert.AreEqual(rootS.TreeStart.SourceJobId, sub1.JobId);
            Assert.AreEqual(rootS.TreeEnd.SourceJobId, root.JobId);

            var sub1S = await _client.GetJobTreeStatisticsAsync(sub1.JobId);
            Assert.IsNotNull(sub1S.ExecutionTime);
            Assert.IsNotNull(sub1S.TreeEnd);
            Assert.IsNotNull(sub1S.TreeStart);
            Assert.AreEqual(sub1S.TreeStart.SourceJobId, subsub1.JobId);
            Assert.AreEqual(sub1S.TreeEnd.SourceJobId, sub1.JobId);
        }

        [TestMethod]
        public async Task TestJobStatisticsAsync2()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("JobStatisticsTestRoot2"));
            var sub1 = await _client.CreateNewJobAsync(new AddJobDto("child1", root.JobId));
            var sub2 = await _client.CreateNewJobAsync(new AddJobDto("child2", root.JobId));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            var subsub1 = await _client.CreateNewJobAsync(new AddJobDto("child1child1", sub1.JobId));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            var rootS = await _client.GetJobTreeStatisticsAsync(root.JobId);
            Assert.IsNotNull(rootS.ExecutionTime);
            Assert.IsNotNull(rootS.TreeEnd);
            Assert.IsNotNull(rootS.TreeStart);
            Assert.AreEqual(rootS.TreeStart.SourceJobId, root.JobId);
            Assert.AreEqual(rootS.TreeEnd.SourceJobId, sub2.JobId);

            var sub1S = await _client.GetJobTreeStatisticsAsync(sub1.JobId);
            Assert.IsNotNull(sub1S.ExecutionTime);
            Assert.IsNotNull(sub1S.TreeEnd);
            Assert.IsNotNull(sub1S.TreeStart);
            Assert.AreEqual(sub1S.TreeStart.SourceJobId, sub1.JobId);
            Assert.AreEqual(sub1S.TreeEnd.SourceJobId, subsub1.JobId);
        }

        [TestMethod]
        public async Task TestJobStatisticsAsync3()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("JobStatisticsTestRoot3"));
            var sub1 = await _client.CreateNewJobAsync(new AddJobDto("child1", root.JobId));
            var sub2 = await _client.CreateNewJobAsync(new AddJobDto("child2", root.JobId));
            await Task.Delay(1000);
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub2.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            var subsub1 = await _client.CreateNewJobAsync(new AddJobDto("child1child1", sub1.JobId));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(subsub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(sub1.JobId, new UpdateJobStateDto(JobState.RanToCompletion));

            var rootS = await _client.GetJobTreeStatisticsAsync(root.JobId);
            Assert.IsNotNull(rootS.ExecutionTime);
            Assert.IsNotNull(rootS.TreeEnd);
            Assert.IsNotNull(rootS.TreeStart);
            Assert.AreEqual(rootS.TreeStart.SourceJobId, sub2.JobId);
            Assert.AreEqual(rootS.TreeEnd.SourceJobId, sub1.JobId);

            var sub1S = await _client.GetJobTreeStatisticsAsync(sub1.JobId);
            Assert.IsNotNull(sub1S.ExecutionTime);
            Assert.IsNotNull(sub1S.TreeEnd);
            Assert.IsNotNull(sub1S.TreeStart);
            Assert.AreEqual(sub1S.TreeStart.SourceJobId, subsub1.JobId);
            Assert.AreEqual(sub1S.TreeEnd.SourceJobId, sub1.JobId);
        }

        [TestMethod]
        public async Task TestBatchAddSubJobsAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("root"));
            var addSubDto = new BatchAddJobDto()
            {
                Children = Enumerable.Range(0, 500).Select(s => new AddJobDto(s.ToString())).ToList(),
                ParentJobId = root.JobId
            };
            var errorRes = await _client.BatchAddChildrenAsync(addSubDto);
            Assert.AreEqual(false, errorRes.Any());
        }

        [TestMethod]
        public async Task TestBatchFlowSubJobsAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto("root"));
            var addSubDto = new BatchAddJobDto()
            {
                Children = Enumerable.Range(0, 1000).Select(s => new AddJobDto(s.ToString())).ToList(),
                ParentJobId = root.JobId
            };
            var errorRes = await _client.BatchAddChildrenAsync(addSubDto);
            Assert.AreEqual(false, errorRes.Any());
            var updateJobBlock = new ActionBlock<long>(async jobId =>
            {
                await _client.UpdateJobStatesAsync(jobId, new UpdateJobStateDto(JobState.Running));
                await _client.UpdateJobStatesAsync(jobId, new UpdateJobStateDto(JobState.RanToCompletion));
            },
                Helper.GetOutOfGrainExecutionOptions());
            foreach (var child in addSubDto.Children)
            {
                await updateJobBlock.PostToBlockUntilSuccessAsync(child.JobId.Value);
            }

            updateJobBlock.Complete();
            await updateJobBlock.Completion;
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.Running));
            await _client.UpdateJobStatesAsync(root.JobId, new UpdateJobStateDto(JobState.RanToCompletion));
        }
        
        [TestMethod]
        public async Task TestDescendantsCountAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = true
            });
            foreach (var index in Enumerable.Range(1, 1))
            {
                var child = await _client.CreateNewJobAsync(new AddJobDto(index.ToString(), root.JobId));
                foreach (var childChildIndex in Enumerable.Range(1, 1))
                {
                    await _client.CreateNewJobAsync(new AddJobDto($"{index}_{childChildIndex}", child.JobId));
                }
            }

            var count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(3, count);

            root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = true
            });
            foreach (var index in Enumerable.Range(1, 10))
            {
                var child = await _client.CreateNewJobAsync(new AddJobDto(index.ToString(), root.JobId));
                foreach (var childChildIndex in Enumerable.Range(1, 1))
                {
                    await _client.CreateNewJobAsync(new AddJobDto($"{index}_{childChildIndex}", child.JobId));
                }
            }

            count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(21, count);

            root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = true
            });
            foreach (var index in Enumerable.Range(1, 5))
            {
                var child = await _client.CreateNewJobAsync(new AddJobDto(index.ToString(), root.JobId));
                foreach (var childChildIndex in Enumerable.Range(1, 3))
                {
                    await _client.CreateNewJobAsync(new AddJobDto($"{index}_{childChildIndex}", child.JobId));
                }
            }

            count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(21, count);

            root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = true
            });
            foreach (var index in Enumerable.Range(1, 10))
            {
                var child = await _client.CreateNewJobAsync(new AddJobDto(index.ToString(), root.JobId));
                foreach (var childChildIndex in Enumerable.Range(1, 3))
                {
                    await _client.CreateNewJobAsync(new AddJobDto($"{index}_{childChildIndex}", child.JobId));
                }
            }

            count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(41, count);
        }

        [TestMethod]
        public async Task TestDescendantsCountInBatchAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = true
            });
            await _client.CreateNewJobAsync(new AddJobDto($"--", root.JobId));
            var addSubDto = new BatchAddJobDto()
            {
                Children = Enumerable.Range(0, 10).Select(s => new AddJobDto(s.ToString())).ToList(),
                ParentJobId = root.JobId
            };
            var errorRes = await _client.BatchAddChildrenAsync(addSubDto);
            addSubDto = new BatchAddJobDto()
            {
                Children = Enumerable.Range(0, 10).Select(s => new AddJobDto(s.ToString())).ToList(),
                ParentJobId = root.JobId
            };
            await _client.BatchAddChildrenAsync(addSubDto);
            var first = addSubDto.Children.First().JobId;
            await _client.CreateNewJobAsync(new AddJobDto("--", first));
            var count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(false, errorRes.Any());
            Assert.AreEqual(23, count);
        }

        [TestMethod]
        public async Task TestSubDescendantsCountAsync()
        {
            var root = await _client.CreateNewJobAsync(new AddJobDto()
            {
                TrackJobCount = false
            });
            var c1 = await _client.CreateNewJobAsync(new AddJobDto($"--", root.JobId)
            {
                TrackJobCount = true
            });
            foreach (var index in Enumerable.Range(1, 10))
            {
                var c11 = await _client.CreateNewJobAsync(new AddJobDto("", c1.JobId));
                var c111 = await _client.CreateNewJobAsync(new AddJobDto("", c11.JobId));
                await _client.CreateNewJobAsync(new AddJobDto("", c111.JobId));
            }

            var count = await _client.GetDescendantsCountAsync(root.JobId);
            Assert.AreEqual(0, count);

            count = await _client.GetDescendantsCountAsync(c1.JobId);
            Assert.AreEqual(31, count);
        }
    }
}