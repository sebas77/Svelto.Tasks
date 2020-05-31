using System.Collections.Generic;

namespace Svelto.Tasks
{
    /// <summary>
    /// Be sure you know what you are doing when you are using the Sync runner, it will stall the current thread!
    /// Depending by the case, it may be better to use the ManualResetEventEx synchronization instead.
    /// </summary>
    namespace Lean
    {
        public class SyncRunner : SyncRunner<IEnumerator<TaskContract>>
        {
            public SyncRunner(int timeout = 1000) : base(timeout)
            {
                _taskCollection = new SerialTaskCollection<IEnumerator<TaskContract>>();
            }
            
            public new void StartCoroutine(in IEnumerator<TaskContract> leanSveltoTask)
            {
                _taskCollection.Clear();
                _taskCollection.Add(leanSveltoTask);
                base.StartCoroutine(_taskCollection);
            }
            
            readonly SerialTaskCollection<IEnumerator<TaskContract>> _taskCollection;
        }
        
        public class SyncRunner<T> : IRunner where T: IEnumerator<TaskContract>
        {
            public bool isStopping { private set; get; }
            public bool isKilled   => false;

            protected SyncRunner(int timeout = 1000)
            {
                _timeout = timeout;
            }

            /// <summary>
            /// todo, this could make sense in a multi-threaded scenario
            /// </summary>
            /// <exception cref="NotImplementedException"></exception>
            public void Pause()
            {
                throw new System.NotImplementedException();
            }

            /// <summary>
            /// todo, this could make sense in a multi-threaded scenario
            /// </summary>
            /// <exception cref="NotImplementedException"></exception>
            public void Resume()
            {
                throw new System.NotImplementedException();
            }

            protected void StartCoroutine(in T task)
            {
                task.Complete(_timeout);
            }

            /// <summary>
            /// todo, this could make sense in a multi-threaded scenario
            /// </summary>
            /// <exception cref="NotImplementedException"></exception>
            public void Stop()
            {
                throw new System.NotImplementedException();
            }

            public void Flush()
            {}
        
            public void Dispose() {}

            public uint numberOfRunningTasks    => 0;
            public uint numberOfQueuedTasks     => 0;
            public uint numberOfProcessingTasks => 0;
        
            readonly int _timeout;
        }
    }
}
