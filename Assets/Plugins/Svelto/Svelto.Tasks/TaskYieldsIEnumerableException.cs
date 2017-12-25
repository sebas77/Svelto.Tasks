using System;

namespace Svelto.Tasks
{
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
    }
}