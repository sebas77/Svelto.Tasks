using System.Collections;

namespace Svelto.Tasks
{
    
    /// <summary>
    /// Be sure you know what you are doing when you are using the Sync runner, it will stall the current thread!
    /// Depending by the case, it may be better to use the ManualResetEventEx synchronization instead. 
    /// </summary>
    public class SyncRunner : SyncRunner<IEnumerator>
    {
        public SyncRunner(int timeout = 1000) : base(timeout)
        {
        }
    }
    public class SyncRunner<T> : IRunner<T>, IEnumerator where T:IEnumerator
    {
        public bool isPaused { get; set; }
        public bool isStopping { private set; get; }
        public bool isKilled { get { return false; } }

        public SyncRunner(int timeout = 1000)
        {
            _timeout = timeout;
        }

        public void StartCoroutine(ISveltoTask<T> task)
        {
            _syncTask = task;
            
            this.Complete(_timeout);
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes it's the final user responsibility to stop the
        /// tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public void Dispose()
        {}
        
        public bool MoveNext()
        {
            return _syncTask.MoveNext();
        }

        public void Reset()
        {
            throw new System.NotImplementedException();
        }

        public int numberOfRunningTasks { get { return 0; } }
        public int numberOfQueuedTasks { get { return 0; } }

        int _timeout;
        ISveltoTask<T> _syncTask;

        public object Current { get; }
    }
}
