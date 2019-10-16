using JobTrackerX.Entities;
using JobTrackerX.Grains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Statistics;
using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace JobTrackerX.WebApi.Services.Background
{
    public class InProcessSilo : IHostedService
    {
        private readonly ISiloHost _silo;
        public readonly IClusterClient Client;

        public InProcessSilo(IOptions<JobTrackerConfig> jobTrackerConfigOptions, ServiceBusWrapper wrapper,
            IndexStorageAccountWrapper accountWrapper)
        {
            var siloConfig = jobTrackerConfigOptions.Value.SiloConfig;
            var jobEntityStorageOptions = new Action<AzureTableStorageOptions>(options =>
            {
                options.ConnectionString = siloConfig.JobEntityPersistConfig.ConnStr;
                options.UseJson = siloConfig.JobEntityPersistConfig.UseJson;
                if (!string.IsNullOrEmpty(siloConfig.JobEntityPersistConfig.TableName))
                {
                    options.TableName = siloConfig.JobEntityPersistConfig.TableName;
                }
            });

            var readOnlyJobIndexStorageOptions = new Action<AzureBlobStorageOptions>(options =>
            {
                options.ConnectionString = siloConfig.ReadOnlyJobIndexPersistConfig.ConnStr;
                options.UseJson = siloConfig.ReadOnlyJobIndexPersistConfig.UseJson;
                if (!string.IsNullOrEmpty(siloConfig.ReadOnlyJobIndexPersistConfig.ContainerName))
                {
                    options.ContainerName = siloConfig.ReadOnlyJobIndexPersistConfig.ContainerName;
                }
            });

            var builder = new SiloHostBuilder()
                .UseLocalhostClustering(11111, 30000, null, siloConfig.ServiceId, siloConfig.ClusterId)
                .ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(JobGrain).Assembly).WithReferences().WithCodeGeneration())
                .AddAzureTableGrainStorage(Constants.JobEntityStoreName, jobEntityStorageOptions)
                .AddAzureTableGrainStorage(Constants.JobRefStoreName, jobEntityStorageOptions)
                .AddAzureTableGrainStorage(Constants.JobIdStoreName, jobEntityStorageOptions)
                .AddAzureTableGrainStorage(Constants.JobIdOffsetStoreName, jobEntityStorageOptions)
                .AddAzureBlobGrainStorage(Constants.ReadOnlyJobIndexStoreName, readOnlyJobIndexStorageOptions)
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => jobTrackerConfigOptions);
                    services.AddSingleton(_ => wrapper);
                    services.AddSingleton(_ => accountWrapper);
                })
                .Configure<GrainCollectionOptions>(options =>
                {
                    options.CollectionAge = siloConfig.GrainCollectionAge ?? TimeSpan.FromMinutes(10);
                    options.ClassSpecificCollectionAge[typeof(AggregateJobIndexGrain).FullName ?? throw new
                                                           InvalidOperationException()] = TimeSpan.FromMinutes(5);
                    options.ClassSpecificCollectionAge[typeof(RollingJobIndexGrain).FullName ?? throw new
                                                           InvalidOperationException()] = TimeSpan.FromMinutes(5);
                })
                .ConfigureLogging(loggingBuilder => loggingBuilder.AddSerilog());
            if (jobTrackerConfigOptions.Value.CommonConfig.UseDashboard)
            {
                builder.UseDashboard(x => x.HostSelf = false);
                builder.UsePerfCounterEnvironmentStatistics();
            }

            _silo = builder.Build();
            Client = _silo.Services.GetRequiredService<IClusterClient>();
            _silo.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _silo.StopAsync(cancellationToken);
        }
    }
}