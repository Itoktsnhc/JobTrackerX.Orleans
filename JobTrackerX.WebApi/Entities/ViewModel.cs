using JobTrackerX.SharedLibs;

namespace JobTrackerX.WebApi.Entities
{
    public class JobIndexViewModel : JobIndex
    {
        public string DetailPath { get { return $"/{JobId}"; } }
        public JobEntityViewModel JobEntity { get; set; }
    }

    public class JobEntityViewModel : JobEntity
    {
        public string ExecutionTimeStr { get; set; }
        public string DetailPath { get { return $"/{JobId}"; } }
        public string AncestorJobPath { get { return $"/{AncestorJobId}"; } }
        public string ParentJobPath { get { return $"/{ParentJobId}"; } }
    }
}
