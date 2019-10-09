# JobTrackerX.Orleans
JobTrackerX.Orleans

基于Azure服务(Table,Blob)和Orleans框架的JobTracker  
项目基于ServiceBus + AzureStorageTable + AzureStorageBlob, 使用MsOrleans作为核心框架。实现了任务的创建、状态更新、多级任务状态联动更新、任务查询等功能

# 项目组成
JobTrackerX.WebApi     -> WebApi项目，宿主了Orleans Silo，MergeJobIndexWorker、IdGenerator等后台服务，并对外暴露了任务相关的API。  
JobTrackerX.Client     -> Http请求的Wrapper。
JobTrackerX.SharedLibs -> WebApi和Client项目共享的部分类

# Big picture
1. 对于每一个任务的记录，在系统中映射为JobGrain，JobId即为JobGrain的long形Id。  
2. 每个JobGrain有自己的状态变化记录。 
3. 使用DescendantsRefGrain来记录父子关系。 
4. 使用JobIdGrain和JobIdOffsetGrain+ServiceBus消息SeqNumber的形式来生成顺序递增的ID。
5. 使用读写分离的ShardJobIndexGrain和AggregateJobIndexGrain以及RollingJobIndexGrain来记录根任务的信息，便于查询。
6. JobGrain和DescendantsRefGrain使用AzureTable存储。
7. ShardJobIndexGrain使用AzureTable存储。  
8. RollingJobIndexGrain和AggregateJobIndexGrain使用了AzureBlob存储。

# Id生成
1. 使用ServiceBus的消息的SequenceNumber属性*一个自定义的ScaleSize来划分每次预申请的Id范围。  
2. 当快超出该范围的时候，重新从ServiceBus中ReceiveAndDelete新的消息，能够保证较高的性能和较低的费用。 
3. 同时后台的IdGenerator服务将监控ServiceBus中消息的数量，并按照一定规则补充消息。 
4. 劣势在于每次重新启动都会划分新的Id范围。

# Job更新
任务状态有以下几种,类似Task的几种状态。
``` CSharp
public enum JobState
{
    WaitingForActivation = 0,
    WaitingToRun = 1,
    Faulted = 2,
    RanToCompletion = 3,
    Running = 4,
    WaitingForChildrenToComplete = 5,
    Warning = 7
}
```
进而JobState可被分为几大类：
``` C#
public enum JobStateCategory
{
    None = 0,
    Pending = 1,
    Success = 2,
    Failed = 3
}
```
之间的关系为：
``` C#
public static JobStateCategory GetJobStateCategory(JobState state)
{
    switch (state)
    {
        case JobState.Running:
        case JobState.WaitingToRun:
        case JobState.Warning:
        case JobState.WaitingForChildrenToComplete:
            return JobStateCategory.Pending;

        case JobState.Faulted:
            return JobStateCategory.Failed;

        case JobState.RanToCompletion:
            return JobStateCategory.Success;

        default:
            return JobStateCategory.None;
    }
}

```
任务、父子任务之间的转换实际上是JobStateCategories的转换，具体请参照JobGrain.cs->UpdateJobStateAsync

# Job索引
1. 任务的索引使用年月日加上小时数作为索引Grain的Id(yyyyMMddHH) 如2019100915，避免单次加载的索引时间范围过大。  
2. 使用读写分离的机制，ShardJobIndexGrain负责处理写入请求。AggregateJobIndexGrain负责处理Query请求。  
3. 其中AggregateJobIndexGrain中使用了RollingJobIndexGrain类似日志中RollingFile+Gzip压缩的方式，降低每次WriteStateAsync到 Blob的网络开销。  
4. ShardJobIndex使用了AzureTableStorage，并且每条索引记录的PartitionKey为 TimeIndex+JobId，以处理大量的写入请求。  
5. MergeJobIndexWorker在后台会按照一定的规则将对应的Table中的索引文件合并、添加到AggregateJobIndexGrain下的RollingJobIndexGrain的State中。  
6. 查询简单的使用了System.Linq.Dynamic.Core库。  

