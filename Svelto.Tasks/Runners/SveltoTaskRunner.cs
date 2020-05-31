using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.DataStructures;

namespace Svelto.Tasks.Internal
{
    public static class SveltoTaskRunner<T>  where T : ISveltoTask
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

        internal class Process<TFlowModifier> : IProcessSveltoTasks where TFlowModifier: IFlowModifier
        {
            public override string ToString()
            {
                return _info.runnerName;
            }

            public Process
            (ThreadSafeQueue<T> newTaskRoutines, FasterList<T> coroutines, FlushingOperation flushingOperation
           , TFlowModifier info)
            {
                _newTaskRoutines   = newTaskRoutines;
                _coroutines        = coroutines;
                _flushingOperation = flushingOperation;
                _info              = info;
            }    

            public bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler) 
                where PlatformProfiler : IPlatformProfiler
            {
                if (_flushingOperation.kill == true)
                {
                    _newTaskRoutines.Clear();
                    _coroutines.Clear();
                    
                    return false;
                }

                if (_flushingOperation.stopping == true && _coroutines.count == 0)
                {
                    //once all the coroutines are flushed the loop can return accepting new tasks
                    _flushingOperation.stopping = false;
                }

                //don't start anything while flushing
                if (_newTaskRoutines.Count > 0 && false == _flushingOperation.stopping)
                    _newTaskRoutines.DequeueAllInto(_coroutines);

                var coroutinesCount = _coroutines.count;
                if (coroutinesCount == 0 ||
                    _flushingOperation.paused == true && _flushingOperation.stopping == false)
                {
                    return true;
                }
                
#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_info.runnerName);
#endif
                _info.Reset();

                //Note: old comment, left as memo, when I used to allow to run tasks immediately
                //I decided to adopt this strategy instead to call MoveNext() directly when a task
                //must be executed immediately. However this works only if I do not update the coroutines count
                //after the MoveNext which on its turn could run immediately another task.
                //this can cause a stack of MoveNext, which works only because I add the task to run immediately
                //at the end of the list and the child MoveNext executes only the new one. When the stack
                //goes back to the previous MoveNext, I don't want to execute the new just added task again,
                //so I can't update the coroutines count, it must stay the previous one/
                int index = 0;

                bool mustExit;

                var coroutines = _coroutines.ToArrayFast(out _);

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
                            Profiler.TaskProfiler.MonitorUpdateDuration(ref coroutines[index], _info.runnerName);
#else
                        result = coroutines[index].MoveNext();
#endif
                    //MoveNext may now cause tasks to run immediately and therefore increase the array size
                    //this side effect is due to the fact that I don't have a stack for each task anymore
                    //like I used to do in Svelto tasks 1.5 and therefore running new enumerators would
                    //mean to add new coroutines. However I do not want to iterate over the new coroutines
                    //during this iteration, so I won't modify coroutinesCount avoid this complexity disabling run
                    //immediate
                    //coroutines = _coroutines.ToArrayFast(out _);

                    int previousIndex = index;

                    if (result == false)
                    {
                        _coroutines.UnorderedRemoveAt(index);

                        coroutinesCount--;
                    }
                    else
                        index++;

                    mustExit = (coroutinesCount == 0 || 
                                _info.CanMoveNext(ref index, ref coroutines[previousIndex], (int) coroutinesCount) ==
                                false ||
                                index >= coroutinesCount);
                } while (!mustExit);

                return true;
            }
            
            readonly ThreadSafeQueue<T> _newTaskRoutines;
            readonly FasterList<T>      _coroutines;
            readonly FlushingOperation  _flushingOperation;
            
            TFlowModifier _info;
        }
        
        public class FlushingOperation
        {
            public bool paused;
            public bool stopping;
            public bool kill;
        }
    }
}