using System.Threading.Tasks;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface IMergeIndexTimer : IGrainWithStringKey
    {
        Task KeepAliveAsync();
    }
}