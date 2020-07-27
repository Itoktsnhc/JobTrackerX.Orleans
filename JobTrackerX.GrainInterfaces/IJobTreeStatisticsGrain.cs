using JobTrackerX.Entities.GrainStates;
using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobTreeStatisticsGrain : IGrainWithIntegerKey
    {
        Task<JobTreeStatisticsState> GetStatisticsAsync();
        Task SetStartAsync(long sourceJobId, DateTimeOffset? timePoint = null);
        Task SetEndAsync(long sourceJobId, DateTimeOffset? timePoint = null);
    }
}
