﻿using JobTrackerX.SharedLibs;

namespace JobTrackerX.WebApi.Entities
{
    public class JobIndexViewModel : JobIndex
    {
        public string DetailPath => $"/{JobId}";
        public string PreviewPath => $"/show/{JobId}/graph";
        public JobEntityViewModel JobEntity { get; set; }
    }

    public class JobEntityViewModel : JobEntity
    {
        public string CalculatedExecutionTimeStr { get; set; }
        public string CalculatedStartTimeStr { get; set; }
        public string CalculatedEndTimeStr { get; set; }
        public string DetailPath { get { return $"/{JobId}"; } }
        public string PreviewPath => $"/show/{JobId}/graph";
        public string AncestorJobPath { get { return $"/{AncestorJobId}"; } }
        public string ParentJobPath { get { return $"/{ParentJobId}"; } }
    }
}
