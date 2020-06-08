using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;
using Orleans;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    public class JobLoggerGrain : Grain, IJobLoggerGrain
    {
        private readonly CloudStorageAccount _account;
        private readonly JobLogConfig _config;

        public JobLoggerGrain(LogStorageAccountWrapper logAccount, IOptions<JobTrackerConfig> options)
        {
            _account = logAccount.Account;
            _config = options.Value.JobLogConfig;
        }
        private CloudBlobClient Client => _account.CreateCloudBlobClient();

        public async Task AppendToJobLogAsync(AppendLogDto dto)
        {
            var container = Client.GetContainerReference(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobReference(this.GetPrimaryKeyLong().ToString());

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes($"{DateTimeOffset.Now}:{dto.Content}{Environment.NewLine}")))
            {
                try
                {
                    if (await appendBlob.ExistsAsync())
                    {
                        await appendBlob.AppendBlockAsync(ms);
                    }
                    else
                    {
                        await appendBlob.UploadFromStreamAsync(ms);
                    }
                }
                catch
                {
                    DeactivateOnIdle();
                    throw;
                }
            }
        }

        public async Task<string> GetJobLogAsync()
        {
            var container = Client.GetContainerReference(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobReference(this.GetPrimaryKeyLong().ToString());
            if (await appendBlob.ExistsAsync())
            {
                using (var ms = new MemoryStream())
                {
                    await appendBlob.DownloadToStreamAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            return null;
        }

        public async Task<string> GetJobLogUrlAsync()
        {
            var container = Client.GetContainerReference(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobReference(this.GetPrimaryKeyLong().ToString());
            if (await appendBlob.ExistsAsync())
            {
                var token = await GetSasTokenAsync(appendBlob, this.GetPrimaryKeyLong().ToString());
                return $"{appendBlob.Uri}{token}";
            }
            return null;
        }

        public async Task<string> GetSasTokenAsync(CloudBlob blob, string downloadName)
        {
            const string policyName = "JOBLOG";
            var permissions = await blob.Container.GetPermissionsAsync();
            if (!permissions.SharedAccessPolicies.TryGetValue(policyName, out var policy) ||
                policy.SharedAccessExpiryTime < DateTimeOffset.Now.AddHours(1))
            {
                policy = new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessExpiryTime = DateTime.UtcNow.AddDays(1),
                    SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-15)
                };
                permissions.SharedAccessPolicies[policyName] = policy;
                await blob.Container.SetPermissionsAsync(permissions);
            }

            var filename = WebUtility.UrlEncode(WebUtility.UrlDecode(downloadName));
            return blob.GetSharedAccessSignature(policy,
                new SharedAccessBlobHeaders()
                {
                    ContentDisposition = "attachment; filename=" + filename + ".log"
                });
        }
    }
}
