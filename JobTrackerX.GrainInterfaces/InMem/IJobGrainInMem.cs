﻿using System.Threading.Tasks;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using Orleans;

namespace JobTrackerX.GrainInterfaces.InMem
{
    public interface IJobGrainInMem : IGrainWithIntegerKey
    {
        Task<JobEntityState> AddJobAsync(AddJobDto addJobDto);

        Task<JobEntityState> GetJobAsync(bool ignoreNotExist = false);

        Task<JobState> GetCurrentJobStateAsync();

        Task UpdateJobStateAsync(UpdateJobStateDto jobStateDto, bool outerCall = true);

        Task OnChildStateChangeAsync(long childJobId, JobState childState);

        Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto);
        Task DeactivateAsync(bool syncState);
    }
}