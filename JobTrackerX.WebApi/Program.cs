using JobTrackerX.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Hosting;
using JobTrackerX.Grains;
using JobTrackerX.Grains.InMem;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Reminders.AzureStorage;
using Itok.Extension.Configuration.AzureBlob;
using JobTrackerX.WebApi.Entities;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Orleans.Persistence.CosmosDB;
using Orleans.Reminders.CosmosDB;
using Orleans.Persistence.CosmosDB.Options;
using Orleans.Runtime;
using Orleans.Statistics;
using System.Runtime.InteropServices;

namespace JobTrackerX.WebApi
{
    public static class Program
    {
        private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddAzureBlobJson(ConfigExtensions.GetJobTrackerConfig())
            .AddEnvironmentVariables()
            .Build();

        public static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();
            try
            {
                ThreadPool.GetMaxThreads(out _, out var completionThreads);
                ThreadPool.SetMinThreads(500, completionThreads);
                await CreateWebHostBuilder(args).Build().RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(config => config.AddAzureBlobJson(ConfigExtensions.GetJobTrackerConfig()))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureAppConfiguration((ctx, cb) =>
                    {
                        StaticWebAssetsLoader.UseStaticWebAssets(
                            ctx.HostingEnvironment,
                            ctx.Configuration);
                    });
                })
                .UseOrleans((context, siloBuilder) =>
                {
                    var jobTrackerConfig =
                        context.Configuration.GetSection(nameof(JobTrackerConfig)).Get<JobTrackerConfig>();
                    var siloConfig = jobTrackerConfig.SiloConfig;
                    var cosmosConfig = jobTrackerConfig.CosmosDbConfig;
                    var cosmosStoreOption = new Action<CosmosDBStorageOptions>(opt =>
                    {
                        opt.DB = cosmosConfig.Database;
                        opt.Collection = cosmosConfig.Container;
                        opt.CanCreateResources = cosmosConfig.CanCreateResource;
                        opt.AccountEndpoint = cosmosConfig.AccountEndpoint;
                        opt.AccountKey = cosmosConfig.AccountKey;
                    });
                    var cosmosReminderOptions = new Action<CosmosDBReminderStorageOptions>(opt =>
                    {
                        opt.DB = cosmosConfig.Database;
                        opt.Collection = cosmosConfig.Container;
                        opt.CanCreateResources = cosmosConfig.CanCreateResource;
                        opt.AccountEndpoint = cosmosConfig.AccountEndpoint;
                        opt.AccountKey = cosmosConfig.AccountKey;
                    });
                    siloBuilder
                        .ConfigureApplicationParts(parts =>
                            parts.AddApplicationPart(typeof(JobGrain).Assembly).WithReferences().WithCodeGeneration())
                        .AddIncomingGrainCallFilter<BufferFilter>()
                        .AddCosmosDBGrainStorage(Constants.JobEntityStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.CounterStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.JobRefStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.JobIdStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.JobIdOffsetStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.ReadOnlyJobIndexStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.AttachmentStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .AddCosmosDBGrainStorage(Constants.AppendStoreName, cosmosStoreOption, typeof(JobTrackerPartitionKeyProvider))
                        .UseCosmosDBReminderService(cosmosReminderOptions)
                        .AddStartupTask(async (sp, token) =>
                        {
                            var factory = sp.GetRequiredService<IGrainFactory>();
                            await factory.GetGrain<IMergeIndexReminder>(Constants.MergeIndexReminderDefaultGrainId)
                                .ActiveAsync();
                            await factory.GetGrain<IMergeIndexTimer>(Constants.MergeIndexTimerDefaultGrainId)
                                .KeepAliveAsync();
                        })
                        .Configure<GrainCollectionOptions>(grainCollectionOptions =>
                        {
                            grainCollectionOptions.CollectionAge =
                                siloConfig.GrainCollectionAge ?? TimeSpan.FromMinutes(10);
                            grainCollectionOptions.ClassSpecificCollectionAge[
                                typeof(AggregateJobIndexGrain).FullName ?? throw new
                                    InvalidOperationException()] = TimeSpan.FromMinutes(5);
                            grainCollectionOptions.ClassSpecificCollectionAge[
                                typeof(RollingJobIndexGrain).FullName ?? throw new
                                    InvalidOperationException()] = TimeSpan.FromMinutes(5);
                        });
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        siloBuilder.UseLinuxEnvironmentStatistics();

                    if (Constants.IsDev)
                    {
                        siloBuilder.UseLocalhostClustering(
                            clusterId: siloConfig.ClusterId,
                            serviceId: siloConfig.ServiceId
                        );
                    }
                    else
                    {
                        siloBuilder.Configure<ClusterOptions>(clusterOptions =>
                            {
                                clusterOptions.ClusterId = siloConfig.ClusterId;
                                clusterOptions.ServiceId = siloConfig.ServiceId;
                            })
                            .UseCosmosDBMembership(opt =>
                            {
                                opt.DB = cosmosConfig.Database;
                                opt.Collection = cosmosConfig.MembershipContainer;
                                opt.CanCreateResources = cosmosConfig.CanCreateResource;
                                opt.AccountEndpoint = cosmosConfig.AccountEndpoint;
                                opt.AccountKey = cosmosConfig.AccountKey;
                            })
                            .ConfigureEndpoints(10001, 10000);
                    }

                    if (jobTrackerConfig.CommonConfig.UseDashboard)
                    {
                        siloBuilder.UseDashboard(x => x.HostSelf = false);
                    }
                })
                .UseSerilog();
        }
    }

    public class JobTrackerPartitionKeyProvider : IPartitionKeyProvider
    {
        public ValueTask<string> GetPartitionKey(string grainType, GrainReference grainReference)
        {
            return ValueTask.FromResult($"{grainType}.{grainReference.GrainIdentity.IdentityString}");
        }
    }
}