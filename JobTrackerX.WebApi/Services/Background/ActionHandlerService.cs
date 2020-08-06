using JobTrackerX.Entities;
using JobTrackerX.WebApi.Services.ActionHandler;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JobTrackerX.GrainInterfaces;
using Orleans;

namespace JobTrackerX.WebApi.Services.Background
{
    public class ActionHandlerService : BackgroundService
    {
        private readonly IClusterClient _orleansClient;
        private readonly ServiceBusWrapper _wrapper;
        private readonly ILogger<ActionHandlerService> _logger;
        private readonly ActionHandlerPool _handlerPool;
        private readonly IOptions<JobTrackerConfig> _jobTrackerConfig;

        public ActionHandlerService(IClusterClient orleansClient, IOptions<JobTrackerConfig> jobTrackerConfig,
            ServiceBusWrapper wrapper, ActionHandlerPool handlerPool, ILogger<ActionHandlerService> logger)
        {
            _orleansClient = orleansClient;
            _wrapper = wrapper;
            _logger = logger;
            _handlerPool = handlerPool;
            _jobTrackerConfig = jobTrackerConfig;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var doneReceiving = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stoppingToken.Register(() => doneReceiving.SetResult(true));

            foreach (var actionQueueClient in _wrapper.ActionQueues)
            {
                var messageHandleOption = new MessageHandlerOptions(OnException)
                {
                    AutoComplete = false,
                    MaxAutoRenewDuration = TimeSpan.FromMinutes(60),
                    MaxConcurrentCalls = _jobTrackerConfig.Value.ActionHandlerConfig.ActionHandlerConcurrent
                };
                actionQueueClient.RegisterMessageHandler(
                    async (message, _) => await OnMessage(message, actionQueueClient),
                    messageHandleOption);
            }

            _logger.LogInformation("ActionHandler Started");
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
                var msgDto = JsonConvert.DeserializeObject<ActionMessageDto>(Encoding.UTF8.GetString(message.Body));
                var grain = _orleansClient.GetGrain<IJobGrain>(msgDto.JobId);
                var relatedJob = await grain.GetJobAsync(true);
                if (relatedJob == null)
                {
                    await client.AbandonAsync(message.SystemProperties.LockToken, new Dictionary<string, object>()
                    {
                        {"dl_reason", $"Grain {msgDto.JobId} No Exist"}
                    });
                    return;
                }

                processResult = await _handlerPool.HandleMessageAsync(msgDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"exception in {nameof(_handlerPool.HandleMessageAsync)}");
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