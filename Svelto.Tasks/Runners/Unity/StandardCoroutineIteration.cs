using System;
using Svelto.DataStructures;

namespace Svelto.Tasks.Unity.Internal
{
    public static class StandardCoroutineProcess
    {
        public static void StandardCoroutineIteration(ref int i, FasterList<IPausableTask> coroutines)
        {
            var pausableTask = coroutines[i];

            bool result;
#if TASKS_PROFILER_ENABLED
            result = Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(pausableTask, _info.runnerName);
#else
            result = pausableTask.MoveNext();
#endif
            if (result == false)
            {
                var disposable = pausableTask as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                coroutines.UnorderedRemoveAt(i--);
            }
        }
    }
}