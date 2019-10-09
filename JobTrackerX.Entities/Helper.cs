using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Entities
{
    public static class Helper
    {
        public static readonly List<JobState> FinishedJobStates
            = new List<JobState>
            {
                JobState.Faulted,
                JobState.RanToCompletion
            };

        public static readonly List<JobState> FinishedOrWaitingForChildrenJobStates =
            new List<JobState>
            {
                JobState.WaitingForChildrenToComplete,
                JobState.Faulted,
                JobState.RanToCompletion
            };

        public static ExecutionDataflowBlockOptions GetOutOfGrainExecutionOptions()
        {
            return new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Constants.DefaultDegreeOfParallelism
            };
        }

        public static IEnumerable<List<T>> SplitListByCount<T>(int nSize, List<T> list)
        {
            for (var i = 0; i < list.Count; i += nSize)
            {
                yield return list.GetRange(i, Math.Min(nSize, list.Count - i));
            }
        }

        public static string GetShardIndexBlobName(JobIndexInner jobIndex, string prefix)
        {
            return $"{prefix}/{jobIndex.JobId}";
        }

        public static string GetShardIndexPartitionKeyName(JobIndexInner jobIndex, string prefix)
        {
            return $"{prefix}-{jobIndex.JobId}";
        }

        public static IEnumerable<int> SplitIntBySize(int size, int val)
        {
            for (var i = 0; i < val / size; i++)
            {
                yield return size;
            }

            var left = val % size;
            if (left > 0)
            {
                yield return left;
            }
        }

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

        /// <summary>
        ///     GetTimeIndex
        /// </summary>
        /// <param name="dt">currentTime if null</param>
        /// <param name="index">default byHour</param>
        /// <returns></returns>
        public static string GetTimeIndex(DateTime? dt = null, TimeIndexType index = TimeIndexType.ByHour)
        {
            var datetime = dt ?? DateTime.UtcNow.AddHours(8);
            return index switch
            {
                TimeIndexType.ByHour => datetime.ToString("yyyyMMddHH"),
                TimeIndexType.ByDay => datetime.ToString("yyyyMMdd"),
                _ => datetime.ToString("yyyyMMddHH"),
            };
        }

        public static List<string> GetTimeIndexRange(DateTimeOffset start, DateTimeOffset end,
            TimeIndexType index = TimeIndexType.ByHour)
        {
            static DateTime AddToCurrentTime(DateTime current, TimeIndexType timeIndexBy)
            {
                return timeIndexBy switch
                {
                    TimeIndexType.ByDay => current.AddDays(1),
                    TimeIndexType.ByHour => current.AddHours(1),
                    _ => current.AddHours(1)
                };
            }

            static DateTime TrimByIndexType(DateTime datetime, TimeIndexType timeIndexBy)
            {
                return timeIndexBy switch
                {
                    TimeIndexType.ByHour => datetime.Date.AddHours(datetime.Hour),
                    TimeIndexType.ByDay => datetime.Date,
                    _ => datetime.Date.AddHours(datetime.Hour)
                };
            }

            var ranges = new List<string>();
            var localStart = TrimByIndexType(start.ToOffset(TimeSpan.FromHours(8)).DateTime, index);
            var localEnd = TrimByIndexType(end.ToOffset(TimeSpan.FromHours(8)).DateTime, index);
            for (var currentTime = localStart;
                currentTime <= localEnd;
                currentTime = AddToCurrentTime(currentTime, index))
            {
                ranges.Add(GetTimeIndex(currentTime, index));
            }

            return ranges;
        }

        public static JobStateCategory GetJobStateCategory(List<StateChangeDto> changes)
        {
            var category = JobStateCategory.None;
            foreach (var itemStateCategory in changes.Select(item =>
                GetJobStateCategory(item.State)))
            {
                switch (itemStateCategory)
                {
                    case JobStateCategory.Failed:
                        category = JobStateCategory.Failed;
                        break;

                    case JobStateCategory.Success when category != JobStateCategory.Failed:
                        category = JobStateCategory.Success;
                        break;

                    case JobStateCategory.Pending
                        when category != JobStateCategory.Failed
                             && category != JobStateCategory.Success:
                        category = JobStateCategory.Pending;
                        break;
                }
            }

            return category;
        }

        public static TReturn GetWrapperStorageAccount<TReturn>(string connStr)
            where TReturn : StorageAccountWrapperBase, new()
        {
            if (!CloudStorageAccount.TryParse(connStr, out var account))
            {
                throw new Exception("Cannot create Storage Account");
            }

            return new TReturn { Account = account };
        }

        public static string GetRollingIndexId(string prefix, int indexCount)
        {
            return $"{prefix}-{indexCount}";
        }
    }

    public static class Extension
    {
        public static async Task PostToBlockUntilSuccessAsync<TInput>(this ITargetBlock<TInput> block, TInput input)
        {
            while (!block.Post(input))
            {
                await Task.Delay(10);
            }
        }
    }
}