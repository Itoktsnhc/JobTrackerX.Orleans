using Itok.Extension.Configuration.AzureBlob;
using JobTrackerX.Entities;
using System;

namespace JobTrackerX.WebApi.Entities
{
    public static class ConfigExtensions
    {
        private static readonly AzureBlobConfigurationOption _azureOptions = new AzureBlobConfigurationOption()
        {
            BlobName = $"appsettings.{Constants.GetEnv()}.json",
            ConnStr = Environment.GetEnvironmentVariable(Constants.ConfigStoreConnStr),
            ContainerName = Environment.GetEnvironmentVariable(Constants.ConfigStoreContainer),
            ReloadOnChange = false
        };

        public static AzureBlobConfigurationOption GetJobTrackerConfig()
        {
            return _azureOptions;
        }
    }
}
