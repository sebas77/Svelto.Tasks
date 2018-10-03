using System.Collections;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public class TaskRunner
    {
        static TaskRunner _instance;

        public static TaskRunner Instance
        {
            get
            {
                if (_instance == null)
                    InitInstance();

                return _instance;
            }
        }

        /// <summary>
        /// Use this function only to preallocate TaskRoutine that can be reused. this minimize run-time allocations
        /// </summary>
        /// <returns>
        /// New reusable TaskRoutine
        /// </returns>
        public ITaskRoutine AllocateNewTaskRoutine()
        {
            return new PausableTask().SetScheduler(_runner);
        }

        public void PauseAllTasks()
        {
            _runner.paused = true;
        }

        public void ResumeAllTasks()
        {
            _runner.paused = false;
        }

        public ContinuationWrapper Run(IEnumerator task)
        {
            return RunOnScheduler(_runner, task);
        }

        /// <summary>
        /// the first instructions until the first yield are executed immediately
        /// </summary>
        /// <param name="runner"></param>
        /// <param name="task"></param>
        /// <returns></returns>
        public ContinuationWrapper RunOnScheduler(IRunner runner, IEnumerator task)
        {
            return _taskPool.RetrieveTaskFromPool().SetScheduler(runner).SetEnumerator(task).Start();
        }

        public static void StopAndCleanupAllDefaultSchedulers()
        {
            StandardSchedulers.KillSchedulers();

            if (_instance != null)
            {
                _instance._taskPool = null;
                _instance._runner   = null;
                _instance = null;
            }
        }

//TaskRunner is supposed to be used in the mainthread only
//this should be enforced in future. 
//Runners should be used directly on other threads 
//than the main one

         static void InitInstance()
         {
            _instance = new TaskRunner();
#if UNITY_5_3_OR_NEWER || UNITY_5
            _instance._runner = StandardSchedulers.coroutineScheduler;
#else
            _instance._runner = new MultiThreadRunner("TaskThread");
#endif
            _instance._taskPool = new PausableTaskPool();

#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
            var debugTasksObject = UnityEngine.GameObject.Find("Svelto.Tasks.Profiler");
            if (debugTasksObject == null)
            {
                debugTasksObject = new UnityEngine.GameObject("Svelto.Tasks.Profiler");
                debugTasksObject.gameObject.AddComponent<Svelto.Tasks.Profiler.TasksProfilerBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(debugTasksObject);
            }
#endif
        }

        IRunner          _runner;
        PausableTaskPool _taskPool;
     }
}
