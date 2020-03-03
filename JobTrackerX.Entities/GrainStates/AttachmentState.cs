using System;
using System.Collections.Generic;
using System.Text;

namespace JobTrackerX.Entities.GrainStates
{
    public class AttachmentState
    {
        public Dictionary<string, string> Body { get; set; } = new Dictionary<string, string>();
    }
}
