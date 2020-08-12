using AutoMapper;
using Blazored.Toast;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using JobTrackerX.WebApi.Entities;
using JobTrackerX.WebApi.Misc;
using JobTrackerX.WebApi.Services.ActionHandler;
using JobTrackerX.WebApi.Services.Attachment;
using JobTrackerX.WebApi.Services.Background;
using JobTrackerX.WebApi.Services.BufferManager;
using JobTrackerX.WebApi.Services.JobTracker;
using JobTrackerX.WebApi.Services.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.Configure<WebUiConfig>(Configuration.GetSection(nameof(WebUiConfig)));
            services.Configure<EmailConfig>(Configuration.GetSection(nameof(EmailConfig)));
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = Constants.BrandName,
                    Version = "1.0.0"
                });
                c.IncludeXmlComments("./JobTrackerX.Web.xml");
            });
            JobTrackerConfig = Configuration.GetSection(nameof(JobTrackerConfig)).Get<JobTrackerConfig>();

            #endregion

            #region BackgroundServices

            // services.AddHostedService<IdGenerator>();
            services.AddHostedService<ActionHandlerService>();
            services.AddHostedService<MergeJobIndexWorker>();
            services.AddHostedService<StateChecker>();

            #endregion

            #region Dependencies

            services.AddHttpClient();
            services.AddBlazoredToast();
            services.AddSingleton<ActionHandlerPool>();
            services.AddSingleton<ServiceBusWrapper>();
            services.AddSingleton(_ =>
                Helper.GetWrapperStorageAccount<IndexStorageAccountWrapper>(
                    JobTrackerConfig.JobIndexConfig.ConnStr));
            services.AddSingleton(_ =>
                Helper.GetWrapperStorageAccount<LogStorageAccountWrapper>(
                    JobTrackerConfig.JobLogConfig.ConnStr));

            services.AddScoped<IQueryIndexService, QueryIndexService>();
            services.AddScoped<IJobTrackerService, JobTrackerService>();
            services.AddScoped<IAttachmentService, AttachmentService>();
            services.AddScoped<IBufferManagerService, BufferManagerService>();

            if (JobTrackerConfig.CommonConfig.UseDashboard)
            {
                services.AddServicesForSelfHostedDashboard();
            }

            services.AddRazorPages();
            services.AddServerSideBlazor()
                .AddCircuitOptions(options => options.DetailedErrors = true);
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
            if (JobTrackerConfig.CommonConfig.UseDashboard)
            {
                app.UseOrleansDashboard(new DashboardOptions {BasePath = "/dashboard"});
            }

            app.UseStaticFiles();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", Constants.BrandName));
            app.UseDeveloperExceptionPage();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        private class MappingProfile : Profile
        {
            public MappingProfile()
            {
                CreateMap<JobEntityState, JobEntity>();
                CreateMap<JobIndexInternal, JobIndex>();
                CreateMap<JobIndex, JobIndexViewModel>();
                CreateMap<JobEntity, JobEntityViewModel>();
                CreateMap<JobTreeStatisticsState, JobTreeStatistics>();
                CreateMap<JobTreeStateItemInternal, JobTreeStateItem>();
                CreateMap<AddToBufferDto, BufferedContent>();
            }
        }
    }
}