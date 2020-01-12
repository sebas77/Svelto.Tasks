using System;
using System.Collections;

namespace Svelto.Tasks
{
    public interface IRunner<T>: IDisposable where T:IEnumerator
    {
        bool    isPaused { get; set; }
        bool    isStopping { get; }
        bool    isKilled { get; }

        void	StartCoroutine(ISveltoTask<T> task);
        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
        int numberOfQueuedTasks { get; }
    }
}
