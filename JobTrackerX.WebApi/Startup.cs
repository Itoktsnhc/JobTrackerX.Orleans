using AutoMapper;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using JobTrackerX.WebApi.Misc;
using JobTrackerX.WebApi.Services.Background;
using JobTrackerX.WebApi.Services.JobTracker;
using JobTrackerX.WebApi.Services.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Orleans;
using OrleansDashboard;

namespace JobTrackerX.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }
        private JobTrackerConfig JobTrackerConfig { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            #region Configurations

            services.AddOptions();
            services.Configure<JobTrackerConfig>(Configuration.GetSection(nameof(JobTrackerConfig)));
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "JobTrackerX",
                    Version = "0.0.1"
                });
                c.IncludeXmlComments("./JobTrackerX.Web.xml");
            });
            JobTrackerConfig = services
                .BuildServiceProvider()
                .GetRequiredService<IOptions<JobTrackerConfig>>().Value;

            #endregion

            #region BackgroundServices

            services.AddSingleton<IHostedService>(c => c.GetRequiredService<InProcessSilo>());
            services.AddHostedService<IdGenerator>();
            services.AddHostedService<MergeJobIndexWorker>();

            #endregion

            #region Dependencies

            services.AddSingleton<InProcessSilo>();
            services.AddSingleton(c => c.GetRequiredService<InProcessSilo>().Client);
            services.AddSingleton<IGrainFactory>(c => c.GetRequiredService<IClusterClient>());
            services.AddSingleton<ServiceBusWrapper>();
            services.AddSingleton(_ =>
                Helper.GetWrapperStorageAccount<IndexStorageAccountWrapper>(
                    JobTrackerConfig.JobIndexConfig.ConnStr));

            services.AddScoped<IQueryIndexService, QueryIndexService>();
            services.AddScoped<IJobTrackerService, JobTrackerService>();

            if (JobTrackerConfig.CommonConfig.UseDashboard)
            {
                services.AddServicesForSelfHostedDashboard();
            }

            services.AddControllers(options =>
            options.Filters.Add(new TypeFilterAttribute(typeof(GlobalExceptionFilter)))).AddNewtonsoftJson();

            #endregion

            #region AutoMappers

            services.AddSingleton(
                new MapperConfiguration(mc => mc.AddProfile(new MappingProfile()))
                    .CreateMapper());

            #endregion
        }

        public void Configure(IApplicationBuilder app)
        {
            if (!string.IsNullOrEmpty(JobTrackerConfig.CommonConfig.AuthToken))
            {
                app.UseMiddleware<TokenAuth>();
            }

            if (JobTrackerConfig.CommonConfig.UseDashboard)
            {
                app.UseOrleansDashboard(new DashboardOptions { BasePath = "/dashboard" });
            }

            app.UseStaticFiles();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobTrackerX.Orleans"));
            app.UseDeveloperExceptionPage();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        private class MappingProfile : Profile
        {
            public MappingProfile()
            {
                CreateMap<JobEntityState, JobEntity>();
                CreateMap<JobIndexInternal, JobIndex>();
            }
        }
    }
}