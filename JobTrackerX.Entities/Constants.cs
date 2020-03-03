namespace JobTrackerX.Entities
{
    public static class Constants
    {
        public const string JobEntityStoreName = "JobStore";
        public const string JobRefStoreName = "JobRefStore";
        public const string ReadOnlyJobIndexStoreName = "ReadOnlyJobIndexStore";
        public const string AttachmentStoreName = "AttachmentStore";

        public const string JobIdOffsetStoreName = "JobIdOffsetStore";
        public const string JobIdStoreName = "JobIdStore";

        public const string JobIdGrainDefaultName = "JobIdGenerator0";
        public const string JobIdOffsetGrainDefaultName = "JobIdOffsetGenerator0";

        public const int DefaultDegreeOfParallelism = 100;

        public const string TokenAuthKey = "x-jobtracker-token";

        public const int GlobalRetryTimes = 3;
        public const int GlobalRetryWaitSec = 1;
        public const string BrandName = "JobTrackerX";

        public const string NotAvailableStr = "--";
        public const string PercentageFormat = "P";
        public const string DecimalFormat = "0.##";
        public const string TagClassStr = "badge badge-dark";
        public const string WebUIInputDateTimeFormat = "yyyy-MM-dd HH";
        public const string WebUIShowDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string WebUIShowTimeSpanFormat = "d'd 'h'h 'm'm 's's'";
#if DEBUG

        public const string EnvName = "Dev";
        public const string SelfDomain = "{YOURDOMAIN}";
#else
        public const string EnvName = "Prod";
        public const string SelfDomain = "{YOURDOMAIN}";

#endif
    }
}