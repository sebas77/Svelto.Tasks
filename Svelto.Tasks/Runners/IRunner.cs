using System;

namespace Svelto.Tasks
{
    public interface IRunner: IDisposable
    {
        bool isStopping { get; }
        bool isKilled   { get; }

        void Pause();
        void Resume();
        void StopAllCoroutines();

        int numberOfRunningTasks { get; }
        int numberOfQueuedTasks  { get; }
        int numberOfProcessingTasks { get; }
    }
}

namespace Svelto.Tasks.Internal
{
    public interface IInternalRunner<T> where T:ISveltoTask
    {
        void StartCoroutine(ref T task, bool immediate);
    }
}