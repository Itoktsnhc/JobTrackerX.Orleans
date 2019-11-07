using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Entities
{
    public static class SharedData
    {
        public static DateTimeOffset? LastMergeTimePoint { get; set; }
    }
}
