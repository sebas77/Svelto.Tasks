
using Svelto.Utilities;

namespace Svelto.Tasks
{
    public class SyncRunner : IRunner
    {
        public bool paused { set; get; }
        public bool isStopping { private set; get; }

        public SyncRunner(bool sleepInBetween = true)
        {}

        public void StartCoroutineThreadSafe(IPausableTask task)
        {
            StartCoroutine(task);
        }

        public void StartCoroutine(IPausableTask task)
        {
            while (task.MoveNext() == true) ThreadUtility.Yield();
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public void Dispose()
        {}

        public int numberOfRunningTasks { get { return -1; } }

        readonly bool _sleepInBetween;
    }
}
