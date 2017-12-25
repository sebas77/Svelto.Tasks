using System;

namespace Svelto.Tasks
{
    public interface IRunner: IDisposable
    {
        bool    paused { get; set; }
        bool    isStopping { get; }

        void	StartCoroutine(IPausableTask task);
        void    StartCoroutineThreadSafe(IPausableTask task);
        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
    }
}
