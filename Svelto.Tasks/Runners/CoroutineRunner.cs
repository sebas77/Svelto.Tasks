using Svelto.Common;
using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    public static class CoroutineRunner<T>  where T : ISveltoTask
    {
        public static void StopRoutines(FlushingOperation flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently. in this way they will be put
            //back to the pool. Let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.stopping = true;
        }
        
        public static void KillProcess(FlushingOperation flushingOperation)
        {
            flushingOperation.kill = true;
        }

        public class Process<TRunningInfo> : IProcessSveltoTasks 
            where TRunningInfo: IRunningTasksInfo
        {
            public Process( ThreadSafeQueue<T> newTaskRoutines, FasterList<T> coroutines,
                            FlushingOperation flushingOperation, TRunningInfo info)
            {
                _newTaskRoutines   = newTaskRoutines;
                _coroutines        = coroutines;
                _flushingOperation = flushingOperation;
                _info              = info;
            }    

            public bool MoveNext<PlatformProfiler>(bool immediate, in PlatformProfiler platformProfiler) 
                where PlatformProfiler : IPlatformProfiler<DisposableSampler>
            {
                if (_flushingOperation.kill) return false;
                {
                    if (_flushingOperation.stopping == true && _coroutines.Count == 0)
                    { //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.stopping = false;
                    }

                    //don't start anything while flushing
                    if (_newTaskRoutines.Count > 0 && false == _flushingOperation.stopping) 
                        _newTaskRoutines.DequeueAllInto(_coroutines);
                    
                    var coroutinesCount = _coroutines.Count;
                    if (coroutinesCount == 0 ||
                        _flushingOperation.paused == true && _flushingOperation.stopping == false)
                    {
                        return true;
                    }
                        
                    _info.Reset();

                    //I decided to adopt this strategy instead to call MoveNext() directly when a task
                    //must be executed immediately. However this works only if I do not update the coroutines count
                    //after the MoveNext which on its turn could run immediately another task.
                    //this can cause a stack of MoveNext, which works only because I add the task to run immediately
                    //at the end of the list and the child MoveNext executes only the new one. When the stack
                    //goes back to the previous MoveNext, I don't want to execute the new just added task again,
                    //so I can't update the coroutines count, it must stay the previous one/
                    int index = immediate == true ? coroutinesCount - 1 : 0;

                    bool mustExit;
                    
                    var coroutines = _coroutines.ToArrayFast();
                    
                    do
                    {
                        if (_info.CanProcessThis(ref index) == false) break;

                        bool result;
                        
                        if (_flushingOperation.stopping) coroutines[index].Stop();

#if ENABLE_PLATFORM_PROFILER
                        using (platformProfiler.Sample(coroutines[index].name))
#endif
#if TASKS_PROFILER_ENABLED
                        result =
                            Profiler.TaskProfiler.MonitorUpdateDuration(ref coroutines[index], _info.runnerName, _profiler);
#else
                        result = coroutines[index].MoveNext();
#endif
                        //MoveNext may now cause tasks to run immediately and therefore increase the array size
                        //this side effect is due to the fact that I don't have a stack for each task anymore
                        //like I used to do in Svelto tasks 1.5 and therefore running new enumerators would
                        //mean to add new coroutines. However I do not want to iterate over the new coroutines
                        //during this iteration, so I won't modify coroutinesCount avoid this complexity disabling run
                        //immediate
                        //coroutines = _coroutines.ToArrayFast();
                        
                        int previousIndex = index;
                        
                        if (result == false)
                        {
                            _coroutines.UnorderedRemoveAt(index);
                            
                            coroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = (coroutinesCount == 0 || immediate || 
                            _info.CanMoveNext(ref index, ref coroutines[previousIndex], coroutinesCount) == false ||
                            index >= coroutinesCount);
                    } 
                    while (!mustExit);
                }

                return true;
            }
            
            readonly ThreadSafeQueue<T> _newTaskRoutines;
            readonly FasterList<T>      _coroutines;
            readonly FlushingOperation  _flushingOperation;
            
            TRunningInfo _info;
        }
        
        public class FlushingOperation
        {
            public bool paused;
            public bool stopping;
            public bool kill;
        }
    }
    
    public struct StandardRunningTasksInfo:IRunningTasksInfo
    {
        public bool CanMoveNext<T>(ref int nextIndex, ref T currentResult, int coroutinesCount)
        {
            return true;
        }

        public bool CanProcessThis(ref int index)
        {
            return true;
        }

        public void Reset()
        {}

        public string runnerName { get; set; }
    }
}