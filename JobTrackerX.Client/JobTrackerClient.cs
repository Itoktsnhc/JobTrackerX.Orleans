using JobTrackerX.SharedLibs;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Client
{
    public class JobTrackerClient : IJobTrackerClient
    {
        private readonly HttpClient _httpClient;

        private readonly Func<HttpRequestMessage, Task> _requestProcessor;
        private readonly int? _retryCount;
        private readonly Func<int, TimeSpan> _getRetryInterval;

        /// <summary>
        /// default httpClient with no proxy
        /// </summary>
        /// <param name="baseAddress"></param>
        public JobTrackerClient(string baseAddress)
        {
            var handler = new HttpClientHandler()
            {
                Proxy = null,
                UseProxy = false
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(baseAddress.EndsWith("/") ? baseAddress : baseAddress + "/", UriKind.Absolute)
            };
        }

        /// <summary>
        /// custom httpClient
        /// </summary>
        /// <param name="client">httpClient</param>
        /// <param name="requestProcessor">modify request</param>
        /// <param name="retryCount">retryPolicy</param>
        /// <param name="retryInterval"></param>
        public JobTrackerClient(HttpClient client,
            Func<HttpRequestMessage, Task> requestProcessor = null,
            int? retryCount = null,
            Func<int, TimeSpan> retryInterval = null)
        {
            _httpClient = client;
            _requestProcessor = requestProcessor;
            _retryCount = retryCount;
            _getRetryInterval = retryInterval;
        }

        public override async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            if (dto.JobId == null)
            {
                dto.JobId = await GetNextIdAsync();
            }

            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                "api/jobTracker/new", dto);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(CreateNewJobAsync)} failed {resp.Msg}");
        }

        public override async Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobStateDto>(HttpMethod.Put,
                $"api/jobTracker/update/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesAsync)} failed {resp.Msg}");
            }
        }

        public override async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobOptionsDto>(HttpMethod.Put,
                $"api/jobTracker/updateOptions/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobOptionsAsync)} failed {resp.Msg}");
            }
        }

        public override async Task<JobEntity> GetJobEntityAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobEntity, object>(HttpMethod.Get,
                $"api/jobTracker/{jobId}");
            return resp.Data;
        }

        public override async Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto)
        {
            var resp = await SendRequestAsync<ReturnQueryIndexDto, QueryJobIndexDto>(HttpMethod.Post,
                "api/QueryIndex",
                dto);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(QueryJobIndexAsync)} failed {resp.Msg}");
        }

        public override async Task<List<JobEntity>> GetDescendantsAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<JobEntity>, object>(HttpMethod.Get,
                $"api/jobTracker/{jobId}/descendants/detail");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetDescendantsAsync)} failed {resp.Msg}");
        }

        public override async Task<List<long>> GetDescendantIdsAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<long>, object>(HttpMethod.Get,
                $"api/jobTracker/{jobId}/descendants");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetDescendantIdsAsync)} failed {resp.Msg}");
        }

        public override async Task<List<JobEntity>> GetChildrenAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<JobEntity>, object>(HttpMethod.Get,
                $"api/jobTracker/{jobId}/children/detail");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetChildrenAsync)} failed {resp.Msg}");
        }

        public override async Task<bool> AppendToJobLogAsync(long jobId, AppendLogDto dto)
        {
            var resp = await SendRequestAsync<string, AppendLogDto>(HttpMethod.Put, $"api/jobTracker/AppendLog/{jobId}",
                dto);
            if (resp.Result)
            {
                return resp.Result;
            }

            throw new Exception($"{nameof(AppendToJobLogAsync)} failed {resp.Msg}");
        }

        public override async Task<JobTreeStatistics> GetJobTreeStatisticsAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobTreeStatistics, object>(HttpMethod.Get,
                $"api/jobTracker/jobTreeStatistics/{jobId}");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(AppendToJobLogAsync)} failed {resp.Msg}");
        }

        public override async Task<List<AddJobErrorResult>> BatchAddChildrenAsync(BatchAddJobDto dto,
            ExecutionDataflowBlockOptions options = null)
        {
            options ??= new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            var assignNewJobIdBlock = new ActionBlock<AddJobDto>(
                async child => child.JobId ??= await GetNextIdAsync(), options);
            foreach (var child in dto.Children)
            {
                assignNewJobIdBlock.Post(child);
            }

            assignNewJobIdBlock.Complete();
            await assignNewJobIdBlock.Completion;

            var resp = await SendRequestAsync<List<AddJobErrorResult>, List<AddJobDto>>(HttpMethod.Post,
                $"/api/JobTracker/newBatch/{dto.ParentJobId}", dto.Children);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(BatchAddChildrenAsync)} failed {resp.Msg}");
        }

        public override async Task<long> GetDescendantsCountAsync(long jobId)
        {
            var resp = await SendRequestAsync<long, object>(HttpMethod.Get,
                $"/api/JobTracker/{jobId}/descendantsCount");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetDescendantsCountAsync)} failed {resp.Msg}");
        }


        public override async Task<JobStateDto> GetJobStateAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobStateDto, object>(HttpMethod.Get, $"/api/JobTracker/{jobId}/state");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetJobStateAsync)} failed {resp.Msg}");
        }

        public override async Task<JobEntityLite> GetJobEntityLiteAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobEntityLite, object>(HttpMethod.Get, $"/api/JobTracker/{jobId}/lite");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetJobEntityLiteAsync)} failed {resp.Msg}");
        }

        #region WithBuffer

        internal override async Task<JobEntity> CreateNewJobWithBufferAsync(AddJobDto dto, Guid bufferId)
        {
            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                $"api/bufferManager/{bufferId}/job/new", dto);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(CreateNewJobWithBufferAsync)} failed {resp.Msg}");
        }

        internal override async Task UpdateJobStatesWithBufferAsync(long id, UpdateJobStateDto dto, Guid bufferId)
        {
            var resp = await SendRequestAsync<string, UpdateJobStateDto>(HttpMethod.Put,
                $"api/bufferManager/{bufferId}/job/update/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesWithBufferAsync)} failed {resp.Msg}");
            }
        }

        internal override async Task UpdateJobOptionsWithBufferAsync(long id, UpdateJobOptionsDto dto, Guid bufferId)
        {
            var resp = await SendRequestAsync<string, UpdateJobOptionsDto>(HttpMethod.Put,
                $"api/bufferManager/{bufferId}/job/updateOptions/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobOptionsWithBufferAsync)} failed {resp.Msg}");
            }
        }

        internal override async Task<JobEntity> GetJobEntityWithBufferAsync(long jobId, Guid bufferId)
        {
            var resp = await SendRequestAsync<JobEntity, object>(HttpMethod.Get,
                $"api/bufferManager/{bufferId}/job/{jobId}");
            return resp.Data;
        }

        internal override async Task<List<BufferedContent>> GetBufferedContentAsync(Guid bufferId)
        {
            var resp = await SendRequestAsync<List<BufferedContent>, object>(HttpMethod.Get,
                $"api/bufferManager/{bufferId}");
            return resp.Data;
        }

        internal override async Task FlushBufferedContentAsync(Guid bufferId)
        {
            var resp = await SendRequestAsync<string, object>(HttpMethod.Post,
                $"api/bufferManager/{bufferId}");
            if (!resp.Result)
            {
                throw new Exception($"{nameof(FlushBufferedContentAsync)} failed {resp.Msg}");
            }
        }

        internal override async Task DiscardBufferedContentAsync(Guid bufferId)
        {
            var resp = await SendRequestAsync<string, object>(HttpMethod.Delete,
                $"api/bufferManager/{bufferId}");
            if (!resp.Result)
            {
                throw new Exception($"{nameof(FlushBufferedContentAsync)} failed {resp.Msg}");
            }
        }

        #endregion

        private async Task<ReturnDto<TData>> SendRequestAsync<TData, TRequestBody>(HttpMethod method, string uri,
            TRequestBody body = default)
        {
            if (_retryCount.HasValue && _getRetryInterval != null)
            {
                return await Policy.Handle<Exception>()
                    .WaitAndRetryAsync(_retryCount.Value, _getRetryInterval)
                    .ExecuteAsync(async () => await SendRequestImplAsync<TData, TRequestBody>(method, uri, body));
            }

            return await SendRequestImplAsync<TData, TRequestBody>(method, uri, body);
        }

        private async Task<ReturnDto<TData>> SendRequestImplAsync<TData, TRequestBody>(HttpMethod method, string uri,
            TRequestBody body = default)
        {
            var req = new HttpRequestMessage(method, uri);
            if (!EqualityComparer<TRequestBody>.Default.Equals(body, default))
            {
                req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }

            var task = _requestProcessor?.Invoke(req);
            if (task != null)
            {
                await task;
            }

            var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
            var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.Headers.TryGetValues(nameof(JobNotFoundException), out var notFound)
                || !notFound.Any()
                || notFound.First() != "true") //not JobNotFoundException, must be success
            {
                resp.EnsureSuccessStatusCode();
            }

            return JsonConvert.DeserializeObject<ReturnDto<TData>>(content);
        }

        public override async Task<long> GetNextIdAsync()
        {
            var resp = await SendRequestAsync<long, object>(HttpMethod.Get, "api/JobTracker/id");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetNextIdAsync)} failed {resp.Msg}");
        }
    }
}