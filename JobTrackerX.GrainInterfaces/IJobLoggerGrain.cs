using JobTrackerX.SharedLibs;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobLoggerGrain : IGrainWithIntegerKey
    {
        Task AppendToJobLogAsync(AppendLogDto dto);
        Task<string> GetJobLogAsync();
        Task<string> GetJobLogUrlAsync();
    }
}