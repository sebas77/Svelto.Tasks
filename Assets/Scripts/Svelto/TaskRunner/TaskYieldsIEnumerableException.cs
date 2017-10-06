using System;
using System.Runtime.Serialization;

namespace Svelto.Tasks
{
    [Serializable]
    public class TaskYieldsIEnumerableException : Exception
    {
        public TaskYieldsIEnumerableException()
        {
        }

        public TaskYieldsIEnumerableException(string message) : base(message)
        {
        }

        public TaskYieldsIEnumerableException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if !NETFX_CORE
        protected TaskYieldsIEnumerableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}