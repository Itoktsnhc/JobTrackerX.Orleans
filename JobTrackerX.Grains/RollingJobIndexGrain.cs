﻿using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Newtonsoft.Json;
using Orleans;
using Orleans.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.ReadOnlyJobIndexStoreName)]
    public class RollingJobIndexGrain : Grain<CompressIndexWrapper>, IRollingJobIndexGrain
    {
        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            if (State?.DataArray != null)
            {
                LoadFromState();
            }
            else
            {
                InternalState = new JobIndexState();
            }
        }

        private JobIndexState InternalState { get; set; }

        public Task<List<JobIndexInternal>> QueryAsync(string queryStr)
        {
            return Task.FromResult(string.IsNullOrEmpty(queryStr)
                ? InternalState.JobIndices.Values.ToList()
                : InternalState.JobIndices.Values.Where(
                    Helper.Interpreter.ParseAsDelegate<Func<JobIndexInternal, bool>>(queryStr, "index")).ToList());
        }

        public async Task MergeIntoIndicesAsync(List<JobIndexInternal> indices)
        {
            foreach (var index in indices)
            {
                InternalState.JobIndices[index.JobId] = index;
            }
            FlushToState();
            await WriteStateAsync();
        }

        public Task<long> GetItemSizeAsync()
        {
            return Task.FromResult<long>(InternalState.JobIndices.Count);
        }

        private void LoadFromState()
        {
            using (var gZipStream = new GZipStream(new MemoryStream(State.DataArray), CompressionMode.Decompress))
            {
                using (var decompressedStream = new MemoryStream())
                {
                    gZipStream.CopyTo(decompressedStream);
                    InternalState = JsonConvert.DeserializeObject<JobIndexState>(Encoding.UTF8.GetString(decompressedStream.ToArray()));
                }
            }
        }

        private void FlushToState()
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(InternalState));
            using (var target = new MemoryStream())
            {
                using (var rawStream = new GZipStream(target, CompressionMode.Compress))
                {
                    rawStream.Write(data, 0, data.Length);
                }
                State.DataArray = target.ToArray();
            }
        }
    }
}