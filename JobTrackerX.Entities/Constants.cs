namespace JobTrackerX.Entities
{
    public static class Constants
    {
        public const string JobEntityStoreName = "JobStore";
        public const string JobRefStoreName = "JobRefStore";
        public const string ReadOnlyJobIndexStoreName = "ReadOnlyJobIndexStore";

        public const string JobIdOffsetStoreName = "JobIdOffsetStore";
        public const string JobIdStoreName = "JobIdStore";

        public const string JobIdGrainDefaultName = "JobIdGenerator0";
        public const string JobIdOffsetGrainDefaultName = "JobIdOffsetGenerator0";

        public const int DefaultDegreeOfParallelism = 100;

        public const string TokenAuthKey = "x-jobtracker-token";
#if DEBUG

        public const string EnvName = "Dev";

#else
        public const string EnvName = "Prod";
#endif
    }
}