using System.Collections;

namespace Svelto.Tasks.Internal
{
    class SyncRunner : IRunner
    {
        public bool paused { set; get; }
        public bool stopped { private set; get; }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines()
        {}

        public void StartCoroutine(IEnumerator task)
        {
            while (task.MoveNext() == true);
        }
    }
}
