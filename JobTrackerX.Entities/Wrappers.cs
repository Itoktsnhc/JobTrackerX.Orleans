using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace JobTrackerX.Entities
{
    public class ServiceBusWrapper
    {
        private static readonly Random _rand = new Random();

        public ServiceBusWrapper(IOptions<JobTrackerConfig> options)
        {
            var idGeneratorConfig = options.Value.IdGeneratorConfig;
            ScaleSize = idGeneratorConfig.ScaleSize;
            CrashDistance = idGeneratorConfig.CrashDistance;
            IdQueueReceiver = new MessageReceiver(idGeneratorConfig.ConnStr, idGeneratorConfig.IdQueueEntityPath,
                ReceiveMode.ReceiveAndDelete);
            IdQueueSender = new MessageSender(idGeneratorConfig.ConnStr, idGeneratorConfig.IdQueueEntityPath);
            ManagementClient = new ManagementClient(idGeneratorConfig.ConnStr);
            ActionQueues =
                idGeneratorConfig.ActionQueues
                .Select(name => new QueueClient(idGeneratorConfig.ConnStr, name)).ToList();
        }

        public IQueueClient GetRandomActionQueueClient()
        {
            int index = _rand.Next(ActionQueues.Count);
            return ActionQueues[index];
        }

        public List<QueueClient> ActionQueues { get; set; } = new List<QueueClient>();
        public IMessageReceiver IdQueueReceiver { get; }
        public int ScaleSize { get; }
        public int CrashDistance { get; }
        public IMessageSender IdQueueSender { get; }
        public ManagementClient ManagementClient { get; }
    }

    public class StorageAccountWrapperBase
    {
        public CloudStorageAccount Account { get; set; }
    }

    public class IndexStorageAccountWrapper : StorageAccountWrapperBase
    {
    }
}