#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks.Unity
{
    /// <summary>
    //SequentialMonoRunner doesn't execute the next
    //coroutine in the queue until the previous one is completed
    /// </summary>
    public class SequentialMonoRunner<T> : MonoRunner<T> where T:IEnumerator
    {
        public SequentialMonoRunner(string name):base(name)
        {
            var info = new UnityCoroutineRunner<T>.RunningTasksInfo { runnerName = name };

            UnityCoroutineRunner<T>.StartUpdateCoroutine(new UnityCoroutineRunner<T>.Process<UnityCoroutineRunner<T>.RunningTasksInfo>
            (_newTaskRoutines, _coroutines, _flushingOperation, info));
        }

        static void SequentialTasksFlushing(
            ThreadSafeQueue<ISveltoTask<IEnumerator>> newTaskRoutines, 
            FasterList<ISveltoTask<IEnumerator>> coroutines, 
            UnityCoroutineRunner<T>.FlushingOperation flushingOperation)
        {
            if (newTaskRoutines.Count > 0 && coroutines.Count == 0)
                newTaskRoutines.DequeueInto(coroutines, 1);
        }
    }
}
#endif