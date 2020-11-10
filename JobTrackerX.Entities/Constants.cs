using System;

namespace JobTrackerX.Entities
{
    public static class Constants
    {
        public const string JobEntityStoreName = "JobStore";
        public const string AppendStoreName = "JobAppendStore";
        public const string JobRefStoreName = "JobRefStore";
        public const string ReadOnlyJobIndexStoreName = "ReadOnlyJobIndexStore";
        public const string AttachmentStoreName = "AttachmentStore";

        public const string JobIdOffsetStoreName = "JobIdOffsetStore";
        public const string JobIdStoreName = "JobIdStore";

        public const string JobIdGrainDefaultName = "JobIdGenerator0";
        public const string JobIdOffsetGrainDefaultName = "JobIdOffsetGenerator0";

        public const int DefaultDegreeOfParallelism = 100;

        public const string TokenAuthKey = "x-jobtracker-token";
        public const string StateCheckerHeaderKey = "x-jobtracker-statecheck";

        public const int GlobalRetryTimes = 3;
        public const int GlobalRetryWaitSec = 1;
        public const string BrandName = "JobTrackerX";
        public const string MergeIndexReminderDefaultGrainId = "MergeIndexReminder";
        public const string MergeIndexTimerDefaultGrainId = "MergeIndexTimer0";

        public const string NotAvailableStr = "--";
        public const string PercentageFormat = "P";
        public const string DecimalFormat = "0.##";
        public const string TagClassStr = "badge badge-dark";
        public const string WebUiInputDateTimeFormat = "yyyy-MM-dd HH";
        public const string WebUiShowDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string WebUiShowTimeSpanFormat = "d'd 'h'h 'm'm 's's'";
        #region Environment

        public const string JobSysEnvKey = "JOBSYS_ENV";
        public const string DevEnvValue = "dev";
        public const string TestEnvValue = "test";
        public const string ProdEnvValue = "prod";

        public static string GetEnv()
        {
            return Environment.GetEnvironmentVariable(JobSysEnvKey, EnvironmentVariableTarget.Process) switch
            {
                TestEnvValue => TestEnvValue,
                ProdEnvValue => ProdEnvValue,
                _ => DevEnvValue
            };
        }

        public static bool IsDev => GetEnv() == DevEnvValue;

        #endregion
        public const string BufferIdKey = "BufferIdKey";
    }
}