using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
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
        private readonly JobLogConfig _config;
        private readonly BlobServiceClient _blobSvc;
        public JobLoggerGrain(LogStorageAccountWrapper logAccount, IOptions<JobTrackerConfig> options)
        {
            _blobSvc = logAccount.BlobSvcClient;
            _config = options.Value.JobLogConfig;
        }

        public async Task AppendToJobLogAsync(AppendLogDto dto)
        {
            var container = _blobSvc.GetBlobContainerClient(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobClient(this.GetPrimaryKeyLong().ToString());

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes($"{DateTimeOffset.Now}:{dto.Content}{Environment.NewLine}")))
            {
                try
                {
                    await appendBlob.CreateIfNotExistsAsync();
                    await appendBlob.AppendBlockAsync(ms);
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
            var container = _blobSvc.GetBlobContainerClient(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobClient(this.GetPrimaryKeyLong().ToString());
            if (await appendBlob.ExistsAsync())
            {
                using (var ms = new MemoryStream())
                {
                    await appendBlob.DownloadToAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            return null;
        }

        public async Task<string> GetJobLogUrlAsync()
        {
            var container = _blobSvc.GetBlobContainerClient(_config.ContainerName);
            await container.CreateIfNotExistsAsync();
            var appendBlob = container.GetAppendBlobClient(this.GetPrimaryKeyLong().ToString());
            if (await appendBlob.ExistsAsync())
            {
                return await GetAccessUrlWithSasTokenAsync(appendBlob, this.GetPrimaryKeyLong().ToString());
            }
            return null;
        }

        public async Task<string> GetAccessUrlWithSasTokenAsync(BlobBaseClient blob, string downloadName)
        {
            //const string policyName = "JOBLOG";
            var filename = WebUtility.UrlEncode(WebUtility.UrlDecode(downloadName));
            if (blob.CanGenerateSasUri)
            {
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blob.BlobContainerName,
                    BlobName = blob.Name,
                    Resource = "b",
                    ContentDisposition = "attachment; filename=" + filename + ".log"
                };
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
                sasBuilder.StartsOn = DateTimeOffset.UtcNow.AddHours(-1);
                sasBuilder.SetPermissions(BlobSasPermissions.Read);
                Uri sasUri = blob.GenerateSasUri(sasBuilder);
                return await Task.FromResult(sasUri.OriginalString);
            }
            throw new Exception($"{blob.Name} cannot GenerateSasUri");
        }
    }
}
