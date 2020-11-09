using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JobTrackerX.SharedLibs;
using Newtonsoft.Json;
using Polly;

namespace JobTrackerX.Client
{
    public class ScaledJobTrackerClient : IScaledJobTrackerClient
    {
        private readonly HttpClient _httpClient;

        private readonly Func<HttpRequestMessage, Task> _requestProcessor;
        private readonly int? _retryCount;
        private readonly Func<int, TimeSpan> _getRetryInterval;

        /// <summary>
        /// default httpClient with no proxy
        /// </summary>
        /// <param name="baseAddress"></param>
        public ScaledJobTrackerClient(string baseAddress)
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
        /// <param name="retryCount"></param>
        /// <param name="getRetryInterval"></param>
        public ScaledJobTrackerClient(HttpClient client,
            Func<HttpRequestMessage, Task> requestProcessor = null,
            int? retryCount = null,
            Func<int, TimeSpan> getRetryInterval = null)
        {
            _httpClient = client;
            _requestProcessor = requestProcessor;
            _retryCount = retryCount;
            _getRetryInterval = getRetryInterval;
        }

        public async Task<long> GetNextIdAsync()
        {
            var resp = await SendRequestAsync<long, object>(HttpMethod.Get,
                "/api/ScaledJob/id");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetNextIdAsync)} failed {resp.Msg}");
        }

        public async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            if (dto.JobId == null)
            {
                dto.JobId = await GetNextIdAsync();
            }

            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                "/api/ScaledJob/new", dto);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(CreateNewJobAsync)} failed {resp.Msg}");
        }

        public async Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobStateDto>(HttpMethod.Put,
                $"/api/ScaledJob/update/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesAsync)} failed {resp.Msg}");
            }
        }

        public async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobOptionsDto>(HttpMethod.Put,
                $"/api/ScaledJob/updateOptions/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobOptionsAsync)} failed {resp.Msg}");
            }
        }

        public async Task<JobEntity> GetJobEntityAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobEntity, object>(HttpMethod.Get,
                $"/api/ScaledJob/{jobId}");
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

        public async Task<List<JobEntity>> GetChildrenAsync(long jobId)
        {
            var resp = await SendRequestAsync<List<JobEntity>, object>(HttpMethod.Get,
                $"/api/ScaledJob/{jobId}/children/detail");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(GetChildrenAsync)} failed {resp.Msg}");
        }

        public async Task<bool> AppendToJobLogAsync(long jobId, AppendLogDto dto)
        {
            var resp = await SendRequestAsync<string, AppendLogDto>(HttpMethod.Put, $"/api/ScaledJob/AppendLog/{jobId}",
                dto);
            if (resp.Result)
            {
                return resp.Result;
            }

            throw new Exception($"{nameof(AppendToJobLogAsync)} failed {resp.Msg}");
        }

        public async Task<JobTreeStatistics> GetJobTreeStatisticsAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobTreeStatistics, object>(HttpMethod.Get,
                $"/api/ScaledJob/jobTreeStatistics/{jobId}");
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(AppendToJobLogAsync)} failed {resp.Msg}");
        }

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
                // ReSharper disable once PossibleMultipleEnumeration
                || !notFound.Any()
                // ReSharper disable once PossibleMultipleEnumeration
                || notFound.First() != "true") //not JobNotFoundException, must be success
            {
                resp.EnsureSuccessStatusCode();
            }

            return JsonConvert.DeserializeObject<ReturnDto<TData>>(content);
        }
    }
}