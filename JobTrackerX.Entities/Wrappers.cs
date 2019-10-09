using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;

namespace JobTrackerX.Entities
{
    public class ServiceBusWrapper
    {
        public ServiceBusWrapper(IOptions<JobTrackerConfig> options)
        {
            var idGeneratorConfig = options.Value.IdGeneratorConfig;
            ScaleSize = idGeneratorConfig.ScaleSize;
            CrashDistance = idGeneratorConfig.CrashDistance;
            Receiver = new MessageReceiver(idGeneratorConfig.ConnStr, idGeneratorConfig.EntityPath,
                ReceiveMode.ReceiveAndDelete);
            Sender = new MessageSender(idGeneratorConfig.ConnStr, idGeneratorConfig.EntityPath);
            ManagementClient = new ManagementClient(idGeneratorConfig.ConnStr);
        }

        public IMessageReceiver Receiver { get; }
        public int ScaleSize { get; }
        public int CrashDistance { get; }
        public IMessageSender Sender { get; }
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