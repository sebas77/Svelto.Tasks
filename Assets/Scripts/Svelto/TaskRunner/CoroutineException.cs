using System;

namespace Svelto.Tasks
{
    public class CoroutineException : Exception
    {
        public CoroutineException(string message, Exception innerException) : base(message, innerException) { }

        public override string StackTrace
        {
            get
            {
                return InnerException.StackTrace;
            }
        }

        public override string ToString()
        {
            return Message + InnerException.Message;
        }
    }
}
