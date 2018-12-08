using System;

namespace Svelto.Tasks
{
    public interface IRunner: IDisposable
    {
        bool    isPaused { get; set; }
        bool    isStopping { get; }
        bool    isKilled { get; }

        void	StartCoroutine(IPausableTask task);
        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
        int numberOfQueuedTasks { get; }
    }
}
