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
        public WebUIConfig WebUIConfig { get; set; }
    }

    public class IdGeneratorConfig
    {
        public string ConnStr { get; set; }
        public string IdQueueEntityPath { get; set; }
        public IList<string> ActionQueues { get; set; }
        public int ScaleSize { get; set; }
        public int CrashDistance { get; set; }
        public int MinMessageCountLeft { get; set; }
        public int MaxMessageCountLeft { get; set; }
        public TimeSpan CheckInterval { get; set; }
    }

    public class CommonConfig
    {
        public bool UseDashboard { get; set; }
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

    public class WebUIConfig
    {
        public bool EnabledRefresh { get; set; }
        public TimeSpan NotificationHideTime { get; set; }
        public TimeSpan UIRefreshInterval { get; set; }
        public TimeSpan FirstUIRefreshDelay { get; set; }
    }

    public class EmailConfig
    {
        public string Account { get; set; }
        public string Password { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
    }
}