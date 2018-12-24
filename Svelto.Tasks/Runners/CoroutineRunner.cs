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

        public class Process<TRunningInfo> : IProcessSveltoTasks where TRunningInfo: IRunningTasksInfo
        {
            public Process( ThreadSafeQueue<T> newTaskRoutines, FasterList<T> coroutines,
                            FlushingOperation flushingOperation, TRunningInfo info)
            {
                _newTaskRoutines   = newTaskRoutines;
                _coroutines        = coroutines;
                _flushingOperation = flushingOperation;
                _info              = info;
            }    

            public bool MoveNext(bool immediate = false)
            {
                if (_flushingOperation.kill) return false;
#if ENABLE_PLATFORM_PROFILER                
                using (var _platformProfiler = new Svelto.Common.PlatformProfiler(_info.runnerName))
#endif
                {
                    //don't start anything while flushing
                    if (_newTaskRoutines.Count > 0 && false == _flushingOperation.stopping) 
                        _newTaskRoutines.DequeueAllInto(_coroutines);
                    
                    var coroutinesCount = _coroutines.Count;
                    
                    if (coroutinesCount == 0 || _flushingOperation.paused == true && _flushingOperation.stopping == false) return true;

                    _info.Reset();

                    //I decided to adopt this strategy instead to call MoveNext() directly when a task
                    //must be executed immediately. However this works only if I do not update the coroutinescount
                    //after the MoveNext which on its turn could run immediately another task.
                    //this can cause a stack of MoveNext, which works only because I add the task to run immediately
                    //at the end of the list and the child MoveNext executes only the new one. When the stack
                    //goes back to the previous MoveNext, I don't want to execute the new just added task again,
                    //so I can't update the coroutines count, it must stay the previous one/
                    int index = immediate == true ? coroutinesCount - 1 : 0;

                    bool mustExit;
                    
                    do
                    {
                        if (_info.CanProcessThis(ref index) == false) break;
                        
                        var coroutines = _coroutines.ToArrayFast();    

                        bool result;
                        
                        if (_flushingOperation.stopping) coroutines[index].Stop();

#if ENABLE_PLATFORM_PROFILER
                        using (_platformProfiler.Sample(coroutines[index].ToString()))
#else
#if TASKS_PROFILER_ENABLED
                            result =
                            Profiler.TaskProfiler.MonitorUpdateDuration(coroutines[index], _info.runnerName);
#else
                            result = coroutines[index].MoveNext();
#endif
#endif
                        int previousIndex = index;
                        
                        if (result == false)
                        {
                            _coroutines.UnorderedRemoveAt(index);
                            coroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = (coroutinesCount == 0 ||
                                    _info.CanMoveNext(ref index, coroutines[previousIndex].Current) == false || index >= coroutinesCount);
                    } 
                    while (!mustExit);
                }

                if (_flushingOperation.stopping == true && _coroutines.Count == 0)
                { //once all the coroutines are flushed the loop can return accepting new tasks
                    _flushingOperation.stopping = false;
                }

                return true;
            }
            
            readonly ThreadSafeQueue<T> _newTaskRoutines;
            readonly FasterList<T>      _coroutines;
            readonly FlushingOperation  _flushingOperation;
            
            TRunningInfo _info;
        }
        
        public struct StandardRunningTasksInfo:IRunningTasksInfo
        {
            public bool CanMoveNext(ref int nextIndex, TaskContract currentResult)
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
    
        public class FlushingOperation
        {
            public bool paused;
            public bool stopping;
            public bool kill;
        }
    }
}