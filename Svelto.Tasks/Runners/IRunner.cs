using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Unity;

namespace Svelto.Tasks
{
    public interface IRunner<T>: IDisposable where T:IEnumerator<TaskContract?>
    {
        bool    isPaused { get; set; }
        bool    isStopping { get; }
        bool    isKilled { get; }

        void	StartCoroutine(ISveltoTask task);
        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
        int numberOfQueuedTasks { get; }
    }
}
