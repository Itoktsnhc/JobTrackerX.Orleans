using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.WebApi.Services.ActionHandler;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;

namespace JobTrackerX.WebApi.Services.Background
{
    public class StateChecker : CoordinatedBackgroundService
    {
        private readonly IClusterClient _orleansClient;
        private readonly ServiceBusWrapper _wrapper;
        private readonly IOptions<JobTrackerConfig> _jobTrackerConfig;
        private readonly ILogger<StateChecker> _logger;
        private readonly ActionHandlerPool _handlerPool;

        public StateChecker(
            IClusterClient orleansClient,
            IOptions<JobTrackerConfig> jobTrackerConfig,
            ActionHandlerPool handlerPool,
            ServiceBusWrapper wrapper,
            ILogger<StateChecker> logger,
            IHostApplicationLifetime appLifetime) : base(appLifetime)
        {
            _orleansClient = orleansClient;
            _wrapper = wrapper;
            _jobTrackerConfig = jobTrackerConfig;
            _logger = logger;
            _handlerPool = handlerPool;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var doneReceiving = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stoppingToken.Register(() => doneReceiving.SetResult(true));

            foreach (var stateCheckQueueClient in _wrapper.StateCheckQueues)
            {
                var messageHandleOption = new MessageHandlerOptions(OnException)
                {
                    AutoComplete = false,
                    MaxAutoRenewDuration = TimeSpan.FromMinutes(60),
                    MaxConcurrentCalls = _jobTrackerConfig.Value.ActionHandlerConfig.StateCheckConcurrent
                };
                stateCheckQueueClient.RegisterMessageHandler(
                    async (message, _) => await OnMessage(message, stateCheckQueueClient),
                    messageHandleOption);
            }

            _logger.LogInformation("StateChecker Started");
            await doneReceiving.Task;
        }

        private Task OnException(ExceptionReceivedEventArgs arg)
        {
            _logger.LogError(arg.Exception, "Receive Msg Error");
            return Task.CompletedTask;
        }

        private async Task OnMessage(Message message, QueueClient client)
        {
            var processResult = false;
            try
            {
                var dto = JsonConvert.DeserializeObject<StateCheckMessageDto>(Encoding.UTF8.GetString(message.Body));
                var jobGrain = _orleansClient.GetGrain<IJobGrain>(dto.JobId);
                var job = await jobGrain.GetJobAsync(true);
                if (job != null)
                {
                    if (dto.StateCheckConfig.TargetStateList.Contains(job.CurrentJobState))
                    {
                        processResult =
                            await _handlerPool.HandleStateCheckerMessageAsync(dto.StateCheckConfig.SuccessfulAction,
                                dto, job);
                    }
                    else
                    {
                        processResult =
                            await _handlerPool.HandleStateCheckerMessageAsync(dto.StateCheckConfig.FailedAction, dto,
                                job);
                    }
                }
                else
                {
                    await client.AbandonAsync(message.SystemProperties.LockToken, new Dictionary<string, object>()
                    {
                        {"dl_reason", $"Grain {dto.JobId} No Exist"}
                    });
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(StateChecker)}");
            }

            if (processResult)
            {
                await client.CompleteAsync(message.SystemProperties.LockToken);
            }
            else
            {
                await client.AbandonAsync(message.SystemProperties.LockToken);
            }
        }
    }
}