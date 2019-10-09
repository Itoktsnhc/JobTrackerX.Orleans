using JobTrackerX.Entities;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.Background
{
    public class IdGenerator : BackgroundService
    {
        private readonly IdGeneratorConfig _config;
        private readonly ILogger<IdGenerator> _logger;
        private readonly ServiceBusWrapper _wrapper;

        public IdGenerator(ServiceBusWrapper wrapper, IOptions<JobTrackerConfig> options, ILogger<IdGenerator> logger)
        {
            _config = options.Value.IdGeneratorConfig;
            _wrapper = wrapper;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queueInfo =
                        await _wrapper.ManagementClient.GetQueueRuntimeInfoAsync(_config.EntityPath, stoppingToken);
                    if (queueInfo.MessageCount < _config.MinMessageCountLeft)
                    {
                        var diff = (int)(_config.MaxMessageCountLeft - queueInfo.MessageCount);
                        foreach (var batch in Helper.SplitIntBySize(100, diff))
                        {
                            await _wrapper.Sender.SendAsync(Enumerable.Range(0, batch).Select(_ => new Message())
                                .ToList());
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"error in {nameof(IdGenerator)}");
                }

                await Task.Delay(_config.CheckInterval, stoppingToken);
            }
        }
    }
}