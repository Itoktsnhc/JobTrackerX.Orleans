using JobTrackerX.SharedLibs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.Client
{
    public class JobTrackerClient
    {
        private readonly string _baseAddress;
        private readonly HttpClient _httpClient;

        public JobTrackerClient(string baseAddress)
        {
            _baseAddress = baseAddress;
            _httpClient = new HttpClient();
        }

        public async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            var resp = await SendRequestAsync<JobEntity, AddJobDto>(HttpMethod.Post,
                $"{_baseAddress}/api/jobTracker/new", dto);
            if (resp.Result)
            {
                return resp.Data;
            }

            throw new Exception($"{nameof(CreateNewJobAsync)} failed");
        }

        public async Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobStateDto>(HttpMethod.Put,
                $"{_baseAddress}/api/jobTracker/update/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesAsync)} failed");
            }
        }

        public async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            var resp = await SendRequestAsync<string, UpdateJobOptionsDto>(HttpMethod.Put,
                $"{_baseAddress}/api/jobTracker/updateOptions/{id}", dto);
            if (!resp.Result)
            {
                throw new Exception($"{nameof(UpdateJobStatesAsync)} failed");
            }
        }

        public async Task<JobEntity> GetJobByIdAsync(long jobId)
        {
            var resp = await SendRequestAsync<JobEntity, object>(HttpMethod.Get,
                $"{_baseAddress}/api/jobTracker/{jobId}");
            return resp.Data;
        }

        public async Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto)
        {
            var resp = await SendRequestAsync<ReturnQueryIndexDto, QueryJobIndexDto>(HttpMethod.Post,
                $"{_baseAddress}/api/QueryIndex",
                dto);
            return resp.Data;
        }

        private async Task<ReturnDto<TData>> SendRequestAsync<TData, TRequestBody>(HttpMethod method, string uri,
            TRequestBody body = default)

        {
            var req = new HttpRequestMessage(method, uri);
            if (!EqualityComparer<TRequestBody>.Default.Equals(body, default))
            {
                req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }

            var resp = await _httpClient.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var content = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ReturnDto<TData>>(content);
        }
    }
}