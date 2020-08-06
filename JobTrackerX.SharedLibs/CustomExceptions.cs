using System;

namespace JobTrackerX.SharedLibs
{
    public class JobNotFoundException : Exception
    {
        public JobNotFoundException(string message) : base(message)
        {
        }
    }
}