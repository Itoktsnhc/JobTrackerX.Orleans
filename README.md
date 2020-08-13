# JobTrackerX.Orleans
[![JetBrainsOpenSource](https://img.shields.io/badge/JB-OpenSource-orange) ](https://www.jetbrains.com/?from=JobTracker.Orleans)  [![Nuget](https://img.shields.io/nuget/dt/JobTrackerX.Client?label=JobTrackerX.Client&logo=Nuget)](https://www.nuget.org/packages/JobTrackerX.Client/)

项目基于[Orleans](https://github.com/dotnet/orleans)分布式框架+azure storage + azure service bus +blazor serverside实现了对于任务状态树的各种状态追踪.
1. 项目组成
    1. 后端部分：JobTrackerX.WebApi
    2. 客户端http SDK：JobTrackerX.Client  [nuget](https://www.nuget.org/packages/JobTrackerX.Client/)
2. 部署方式
    1. 当前项目支持单节点部署。后期会添加多节点部署（k8s）部署模式。
        1. 部署后端项目 JobTrackerX.WebApi
        2. 部署前端项目
3. 概览：
    本项目支持树形任务结构，比如 
    ```
                 root
                /    \
               /      \
              /        \
           child1     child2
          /      \
         /        \
    child1Child1 child1Child2
    ```
    每个节点都有自己的状态
    ```csharp
        WaitingForActivation = 0,//初始状态，当任务未创建时
        WaitingToRun = 1,//创建任务后的状态
        Faulted = 2,//失败
        RanToCompletion = 3,//成功完成
        Running = 4,//运行中
        WaitingForChildrenToComplete = 5,//等待子任务完成
        Warning = 7//警告
    ```
    每个节点的状态只有当自身以及后继节点的状态都为完成(RanToCompletion/Faulted)时,自身状态会按 所有后继节点是否有成功&&自身任务状态是否成功 作为当前节点的实际状态。  

    支持状态检查以及任务处于某个状态触发回调功能。



4. JobTrackerClient 示例：
    1. 假设后端地址为const string baseUrl =  "http://jobtracker.example.com"
    2. 示例代码:
        ```csharp
            var client = new JobTrackerClient(baseUrl);
            var rootJob = await client.CreateNewJobAsync(new AddJobDto { JobName = "RootJob" });
            await client.UpdateJobStatesAsync(rootJob.JobId, new UpdateJobStateDto(JobState.Running, "rootJobRunning"));
            var child1 = await client.CreateNewJobAsync(new AddJobDto("child1", rootJob.JobId));
            var child2 = await client.CreateNewJobAsync(new AddJobDto("child2", rootJob.JobId));
            await client.UpdateJobStatesAsync(rootJob.JobId, new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            await client.UpdateJobStatesAsync(rootJob.JobId, new UpdateJobStateDto(JobState.Running, "rootJobRunningAgain"));
            await client.UpdateJobStatesAsync(rootJob.JobId, new UpdateJobStateDto(JobState.RanToCompletion, "rootJobFinished"));
            rootJob = await client.GetJobEntityAsync(rootJob.JobId);

            await client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.Warning, "child1 Running"));
            await client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.Running, "child2 Running"));

            await client.UpdateJobStatesAsync(child1.JobId, new UpdateJobStateDto(JobState.RanToCompletion, "child1 finished"));
            await client.UpdateJobStatesAsync(child2.JobId, new UpdateJobStateDto(JobState.RanToCompletion, "child2 finished"));
        ```
5. JobTrackerContext示例:
    1. JobTrackerContext 实现了对于'持久化'延迟，可以大幅提高大量任务创建的性能.
    2. 示例：
        ```csharp
            var sw = new Stopwatch();
            sw.Start();
            var count = 1000;
            var context = new JobTrackerContext(_client);
            var root = await context.CreateNewJobAsync(new AddJobDto("InBuffer"));
            var batchOp = new JobTrackerBatchOperation(new ExecutionDataflowBlockOptions()
                {MaxDegreeOfParallelism = 10});
            foreach (var index in Enumerable.Range(0, count))
            {
                batchOp.Add(async () =>
                {
                    var c = await context.CreateNewJobAsync(new AddJobDto($"c-{index}", root.JobId));
                    await context.UpdateJobStatesAsync(c.JobId, new UpdateJobStateDto(JobState.Running, "--"));
                    await context.UpdateJobStatesAsync(c.JobId,
                        new UpdateJobStateDto(JobState.RanToCompletion, "--"));
                });
            }

            await context.BatchOperationAsync(batchOp);
            var inMem = sw.Elapsed;
            Console.WriteLine($"inMem {inMem} on {count}");
            sw.Stop();
            Console.ReadLine();
            sw.Start();
            await context.CommitAndCloseAsync();
            sw.Stop();
            var all = sw.Elapsed;
            Console.WriteLine($"flush use {all - inMem}");
            Console.WriteLine($"all use {all} on {count}");

        ```
6. 回调功能：
    创建任务的时候支持传入 ActionConfigs 包含了 '当任务处于x状态' 做 y事情的配置，
    例如： 
    当任务状态出错的时候，发送一封邮件给xxx.
    当任务状态成功，调用外部的api.
7. 状态检查：
    当在未来一个时间点，检查当前任务是否处于特定的状态，检查成功，做x事情，检查失败做y事情
