using System;

namespace JobTrackerX.Entities
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface)]
    public class BufferInMemAttribute : Attribute
    {
    }
}