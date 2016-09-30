using System;
using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Internal;

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

    public void Run(Func<IEnumerator> taskGenerator)
    {
        _taskPool.RetrieveTaskFromPool().SetScheduler(_runner).SetEnumeratorProvider(taskGenerator).Start();
    }

    public void Run(IEnumerator taskGenerator)
    {
        _taskPool.RetrieveTaskFromPool().SetScheduler(_runner).SetEnumerator(taskGenerator).Start();
    }

    public void RunOnSchedule(IRunner runner, Func<IEnumerator> taskGenerator)
    {
        _taskPool.RetrieveTaskFromPool().SetScheduler(runner).SetEnumeratorProvider(taskGenerator).Start();
    }

    public void RunOnSchedule(IRunner runner, IEnumerator taskGenerator)
    {
        _taskPool.RetrieveTaskFromPool().SetScheduler(runner).SetEnumerator(taskGenerator).Start();
    }

    public void StopDefaultSchedulerTasks()
    {
        StandardSchedulers.StopSchedulers();
    }

    public void StopAndCleanupAllDefaultSchedulerTasks()
    {
        StopDefaultSchedulerTasks();

        _taskPool = null;
        _runner = null;
        _instance = null;
    }

//TaskRunner is supposed to be used in the mainthread only
//this should be enforced in future. 
//Runners should be used directly on other threads 
//than the main one

    static void InitInstance()
    {
        _instance = new TaskRunner();
#if UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
        _instance._runner = StandardSchedulers.mainThreadScheduler;
#else
        _instance._runner = new MultiThreadRunner();
#endif
        _instance._taskPool = new PausableTaskPool();
    }

    IRunner             _runner;
    PausableTaskPool    _taskPool;
}
