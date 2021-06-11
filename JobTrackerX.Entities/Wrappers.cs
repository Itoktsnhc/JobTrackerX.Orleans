using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Storage.Blobs;

namespace JobTrackerX.Entities
{
    public class ServiceBusWrapper
    {
        private static readonly Random Rand = new Random();

        public ServiceBusWrapper(IOptions<JobTrackerConfig> options)
        {
            var actionHandlerConfig = options.Value.ActionHandlerConfig;
            ActionQueues =
                actionHandlerConfig.ActionQueues
                .Select(name => new QueueClient(actionHandlerConfig.ConnStr, name)).ToList();
            StateCheckQueues =
                actionHandlerConfig.StateCheckQueues
                .Select(name => new QueueClient(actionHandlerConfig.ConnStr, name)).ToList();
        }

        public IQueueClient GetRandomActionQueueClient()
        {
            int index = Rand.Next(ActionQueues.Count);
            return ActionQueues[index];
        }
        public IQueueClient GetRandomStateCheckQueueClient()
        {
            int index = Rand.Next(StateCheckQueues.Count);
            return StateCheckQueues[index];
        }

        public List<QueueClient> ActionQueues { get; set; } = new List<QueueClient>();
        public List<QueueClient> StateCheckQueues { get; set; } = new List<QueueClient>();
    }

    public class StorageAccountWrapper
    {
        public BlobServiceClient BlobSvcClient { get; set; }
        public Microsoft.Azure.Cosmos.Table.CloudStorageAccount TableAccount { get; set; }
    }

    public class IndexStorageAccountWrapper : StorageAccountWrapper
    {
    }

    public class LogStorageAccountWrapper : StorageAccountWrapper
    {
    }
}