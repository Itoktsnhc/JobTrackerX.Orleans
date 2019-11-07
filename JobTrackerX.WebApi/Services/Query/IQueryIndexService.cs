using JobTrackerX.SharedLibs;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.Query
{
    public interface IQueryIndexService
    {
        Task<ReturnQueryIndexDto> QueryJobsAsync(QueryJobIndexDto dto);
    }
}