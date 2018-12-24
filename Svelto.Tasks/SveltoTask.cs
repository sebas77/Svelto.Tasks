using System;

namespace Svelto.Tasks
{
    public class SveltoTaskException : Exception
    {
        public SveltoTaskException(Exception e)
            : base(e.ToString(), e)
        {
        }

        public SveltoTaskException(string message, Exception e)
            : base(message.FastConcat(" -", e.ToString()), e)
        {
        }
    }

    public interface ISveltoTask
    {
        TaskContract Current { get; }

        bool MoveNext();

        void Stop();
    }
}

