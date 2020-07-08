using System;

namespace JobTrackerX.SharedLibs
{
    public class JobNotFoundException : Exception
    {
        public JobNotFoundException(string message) : base(message)
        {
        }

        public JobNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public JobNotFoundException() : base()
        {
        }
    }
}