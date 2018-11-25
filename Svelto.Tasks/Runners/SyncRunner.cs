using System;
using Svelto.Utilities;

namespace Svelto.Tasks
{
    
    /// <summary>
    /// Be sure you know what you are doing when you are using the Sync runner, it will stall the current thread!
    /// Depending by the case, it may be better to use the ManualResetEventEx synchronization instead. 
    /// </summary>
    public class SyncRunner : IRunner
    {
        public bool paused { set; get; }
        public bool isStopping { private set; get; }
        public bool isKilled { get { return false; } }

        public SyncRunner(int timeout = 1000)
        {
            _timeout = timeout;
        }

        public void StartCoroutine(IPausableTask task)
        {
            var quickIterations = 0;
            
            DateTime then = DateTime.Now.AddMilliseconds(_timeout);
            bool valid = true;
            
            while (task.MoveNext() && (valid = (DateTime.Now < then)))
                ThreadUtility.Wait(ref quickIterations);
            
            if (valid == false)
                throw new Exception("SyncRunner timed out, increase time out or check if got stuck");
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes it's the final user responsibility to stop the
        /// tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public void Dispose()
        {}

        public int numberOfRunningTasks { get { return -1; } }
        
        int _timeout;
    }
}
