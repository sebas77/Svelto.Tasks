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
        public ITaskRoutine<IEnumerator> AllocateNewTaskRoutine()
        {
            return new TaskRoutine<IEnumerator>(StandardSchedulers.standardScheduler);
        }
        
        public ITaskRoutine<T> AllocateNewTaskRoutine<T>(IRunner<T> runner) where T:IEnumerator
        {
            return new TaskRoutine<T>(runner);
        }
        
        public ITaskRoutine<IEnumerator> AllocateNewTaskRoutine(IRunner<IEnumerator> runner)
        {
            return new TaskRoutine<IEnumerator>(runner);
        }

        public void PauseAllTasks()
        {
            StandardSchedulers.standardScheduler.isPaused = true;
        }

        public void ResumeAllTasks()
        {
            StandardSchedulers.standardScheduler.isPaused = false;
        }

        public IContinuationWrapper Run(IEnumerator task)
        {
            return RunOnScheduler(StandardSchedulers.standardScheduler, task);
        }

        /// <summary>
        /// the first instructions until the first yield are executed immediately
        /// </summary>
        /// <param name="runner"></param>
        /// <param name="task"></param>
        /// <returns></returns>
        public IContinuationWrapper RunOnScheduler(IRunner<IEnumerator> runner, IEnumerator task) 
        {
            return _taskPool.RetrieveTaskFromPool().Start(runner, task);
        }

        public static void StopAndCleanupAllDefaultSchedulers()
        {
            StandardSchedulers.KillSchedulers();

            if (_instance != null)
            {
                _instance._taskPool = null;
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
            _instance._taskPool = new SveltoTasksPool();

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

        SveltoTasksPool _taskPool;
     }
}
