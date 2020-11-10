using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;

namespace JobTrackerX.Grains
{
    public class MergeIndexReminder : Grain, IMergeIndexReminder, IRemindable
    {
        private readonly CloudStorageAccount _account;
        private readonly IndexConfig _indexConfig;
        private readonly ILogger<MergeIndexReminder> _logger;
        private readonly string _tableName;
        private CloudTableClient Client => _account.CreateCloudTableClient();

        public MergeIndexReminder(ILogger<MergeIndexReminder> logger,
            IOptions<JobTrackerConfig> config, IndexStorageAccountWrapper wrapper)
        {
            _logger = logger;
            _account = wrapper.TableAccount;
            _indexConfig = config.Value.JobIndexConfig;
            _tableName = _indexConfig.TableName;
        }

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            await RegisterOrUpdateReminder(Constants.MergeIndexReminderDefaultGrainId, TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(3));
        }

        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            await KeepTimerAliveAsync();
        }

        public Task ActiveAsync()
        {
            return Task.CompletedTask;
        }

        private async Task KeepTimerAliveAsync()
        {
            var timer = GrainFactory.GetGrain<IMergeIndexTimer>(Constants.MergeIndexTimerDefaultGrainId);
            await timer.KeepAliveAsync();
        }
    }
}