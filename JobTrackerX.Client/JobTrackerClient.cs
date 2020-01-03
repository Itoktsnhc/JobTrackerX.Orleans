using JobTrackerX.SharedLibs;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.Client
{
    public class JobTrackerClient : IJobTrackerClient
    {
        private readonly HttpClient _httpClient;

        private readonly Func<HttpRequestMessage, Task> _requestProcessor;
        private readonly AsyncRetryPolicy _retryPolicy;

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
        /// <param name="retryPolicy">retryPolicy</param>
        public JobTrackerClient(HttpClient client,
            Func<HttpRequestMessage, Task> requestProcessor = null,
            AsyncRetryPolicy retryPolicy = null)
        {
            _httpClient = client;
            _requestProcessor = requestProcessor;
            _retryPolicy = retryPolicy;
        }

        public async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                "api/jobTracker/new", dto);
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(CreateNewJobAsync)} failed {resp.Msg}");
        }

        public async Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobStateDto>(HttpMethod.Put,
                $"api/jobTracker/update/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesAsync)} failed {resp.Msg}");
            }
        }

        public async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobOptionsDto>(HttpMethod.Put,
                $"api/jobTracker/updateOptions/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobOptionsAsync)} failed {resp.Msg}");
            }
        }

        public async Task<JobEntity> GetJobEntityAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobEntity, object>(HttpMethod.Get,
                $"api/jobTracker/{jobId}");
            return resp.Data;
        }

        public async Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto)
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

        public async Task<List<JobEntity>> GetDescendantsAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<JobEntity>, object>(HttpMethod.Get, $"api/jobTracker/{jobId}/descendants/detail");
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(GetDescendantsAsync)} failed {resp.Msg}");
        }

        public async Task<List<long>> GetDescendantIdsAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<long>, object>(HttpMethod.Get, $"api/jobTracker/{jobId}/descendants");
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(GetDescendantIdsAsync)} failed {resp.Msg}");
        }

        public async Task<List<JobEntity>> GetChildrenAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<JobEntity>, object>(HttpMethod.Get, $"api/jobTracker/{jobId}/children/detail");
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(GetChildrenAsync)} failed {resp.Msg}");
        }

        private async Task<ReturnDto<TData>> SendRequestAsync<TData, TRequestBody>(HttpMethod method, string uri,
            TRequestBody body = default)
        {
            if (_retryPolicy == null)
            {
                return await SendRequestImplAsync<TData, TRequestBody>(method, uri, body);
            }
            return await _retryPolicy.ExecuteAsync(
                async () => await SendRequestImplAsync<TData, TRequestBody>(method, uri, body));
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
                || notFound.First() != "true")//not JobNotFoundException, must be success
            {
                resp.EnsureSuccessStatusCode();
            }
            return JsonConvert.DeserializeObject<ReturnDto<TData>>(content);
        }
    }
}