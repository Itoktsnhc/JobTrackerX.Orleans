using System.Threading.Tasks;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface IMergeIndexReminder : IGrainWithStringKey
    {
        Task ActiveAsync();
    }
}