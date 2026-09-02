using System;

namespace Svelto.Tasks.Parallelism
{
    public interface ISveltoJob: IDisposable
    {
        void Update(int jobIndex);
    }
}