using JobTrackerX.SharedLibs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.Client
{
    public class JobTrackerClient : IJobTrackerClient
    {
        private readonly HttpClient _httpClient;

        private readonly Func<HttpRequestMessage, Task> _requestProcessor;

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
        public JobTrackerClient(HttpClient client, Func<HttpRequestMessage, Task> requestProcessor = null)
        {
            _httpClient = client;
            _requestProcessor = requestProcessor;
        }

        public async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                $"api/jobTracker/new", dto);
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
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(GetJobEntityAsync)} failed {resp.Msg}");
        }

        public async Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto)
        {
            var resp = await SendRequestAsync<ReturnQueryIndexDto, QueryJobIndexDto>(HttpMethod.Post,
                $"api/QueryIndex",
                dto);
            if (resp.Result)
            {
                return resp.Data;
            }
            throw new Exception($"{nameof(QueryJobIndexAsync)} failed {resp.Msg}");
        }

        private async Task<ReturnDto<TData>> SendRequestAsync<TData, TRequestBody>(HttpMethod method, string uri,
            TRequestBody body = default)

        {
            var req = new HttpRequestMessage(method, uri);
            if (!EqualityComparer<TRequestBody>.Default.Equals(body, default))
            {
                req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }
            await _requestProcessor?.Invoke(req);
            var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<ReturnDto<TData>>(content);
        }
    }
}