
namespace Svelto.Tasks.Internal
{
    class SyncRunner : IRunner
    {
        public bool paused { set; get; }
        public bool stopped { private set; get; }

        public void StartCoroutineThreadSafe(IPausableTask task)
        {
            StartCoroutine(task);
        }

        public void StartCoroutine(IPausableTask task)
        {
            while (task.MoveNext() == true);
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public int numberOfRunningTasks { get { return -1; } }
    }
}
