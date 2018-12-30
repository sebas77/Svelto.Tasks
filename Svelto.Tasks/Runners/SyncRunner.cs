using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// Be sure you know what you are doing when you are using the Sync runner, it will stall the current thread!
    /// Depending by the case, it may be better to use the ManualResetEventEx synchronization instead. 
    /// </summary>
    public class SyncRunner : SyncRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        public SyncRunner(int timeout = 1000) : base(timeout)
        {
        }
    }
    public class SyncRunner<T> : IRunner, IInternalRunner<T> where T: ISveltoTask
    {
        public bool isStopping { private set; get; }
        public bool isKilled { get { return false; } }
        
        public void Pause()
        {
            throw new System.NotImplementedException();
        }

        public void Resume()
        {
            throw new System.NotImplementedException();
        }

        public SyncRunner(int timeout = 1000)
        {
            _timeout = timeout;
        }

        public void StartCoroutine(ref T task, bool immediate)
        {
            TaskRunnerExtensions.CompleteTask(ref task, _timeout);
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes it's the final user responsibility to stop the
        /// tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public int numberOfRunningTasks { get { return 0; } }
        public int numberOfQueuedTasks { get { return 0; } }
        public int numberOfProcessingTasks { get { return 0; } }
        public void Dispose() {}

        readonly int _timeout;
    }
}
