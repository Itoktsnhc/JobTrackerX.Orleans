using System;
using System.Collections.Generic;

namespace JobTrackerX.Entities
{
    public class JobTrackerConfig
    {
        public SiloConfig SiloConfig { get; set; }
        public CommonConfig CommonConfig { get; set; }
        public IdGeneratorConfig IdGeneratorConfig { get; set; }
        public IndexConfig JobIndexConfig { get; set; }
        public WebUiConfig WebUiConfig { get; set; }
        public JobLogConfig JobLogConfig { get; set; }
        public ActionHandlerConfig ActionHandlerConfig { get; set; }
        
        public AzureClusterConfig AzureClusterConfig { get; set; }
    }
    
    public class AzureClusterConfig
    {
        public string ConnStr { get; set; }
        public string TableName { get; set; }
    }

    public class IdGeneratorConfig
    {
        public int ScaleSize { get; set; }
        public int CrashDistance { get; set; }
    }

    public class ActionHandlerConfig
    {
        public int ActionHandlerConcurrent { get; set; } = 10;
        public int StateCheckConcurrent { get; set; } = 10;
        public IList<string> ActionQueues { get; set; }
        public string ConnStr { get; set; }
        public List<string> StateCheckQueues { get; set; }
    }

    public class CommonConfig
    {
        public bool UseDashboard { get; set; }
        public bool BlockStateUpdateAfterFinished { get; set; }
    }

    public class IndexConfig
    {
        public string ConnStr { get; set; }
        public string TableName { get; set; }
        public TimeSpan IndexMergeInterval { get; set; } = TimeSpan.FromMinutes(10);
        public int TrackTimeIndexCount { get; set; }
        public long MaxRoundSize { get; set; }
        public long MaxRollingSize { get; set; }
    }

    public class SiloConfig
    {
        public GrainPersistConfig JobEntityPersistConfig { get; set; }
        public GrainPersistConfig ReadOnlyJobIndexPersistConfig { get; set; }
        public GrainPersistConfig ReminderPersistConfig { get; set; }
        public TimeSpan? GrainCollectionAge { get; set; }
        public string ServiceId { get; set; }
        public string ClusterId { get; set; }
    }

    public class GrainPersistConfig
    {
        public string ConnStr { get; set; }
        public string TableName { get; set; }
        public string ContainerName { get; set; }
    }

    public class WebUiConfig
    {
        public bool EnabledRefresh { get; set; }
        public TimeSpan NotificationHideTime { get; set; }
        public TimeSpan UiRefreshInterval { get; set; }
        public TimeSpan FirstUiRefreshDelay { get; set; }
    }

    public class JobLogConfig
    {
        public string ConnStr { get; set; }
        public string ContainerName { get; set; }
    }

    public class EmailConfig
    {
        public string Account { get; set; }
        public string Password { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
    }
}