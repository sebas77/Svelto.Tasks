using System;

namespace Svelto.Tasks
{
    public interface IRunner: IDisposable
    {
        bool isStopping { get; }
        bool isKilled   { get; }

        void Pause();
        void Resume();
        void Stop();
        void Flush();

        int numberOfRunningTasks { get; }
        int numberOfQueuedTasks  { get; }
        int numberOfProcessingTasks { get; }
    }

    public interface IRunner<T> where T:ISveltoTask
    {
        void StartCoroutine(ref T task/*, bool immediate*/);
    }
}
