using JobTrackerX.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace JobTrackerX.WebApi
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            ThreadPool.GetMaxThreads(out _, out var completionThreads);
            ThreadPool.SetMinThreads(500, completionThreads);
            CreateWebHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(config => config.AddJsonFile($"appsettings.{Constants.EnvName}.json"))
                .ConfigureLogging((_, logging) =>
                {
                    logging.SetMinimumLevel(LogLevel.Error);
                    logging.AddConsole();
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
        }
    }
}