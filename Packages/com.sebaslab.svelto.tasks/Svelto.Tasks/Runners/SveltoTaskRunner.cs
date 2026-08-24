//#define DEBUG_TASKS_FLOW

using System;
using System.Collections.Concurrent;
using System.Threading;
using Svelto.Common;
using Svelto.DataStructures;

namespace Svelto.Tasks.Internal
{
    //ISveltoTask can be Lean or ExtraLean
    public static class SveltoTaskRunner<TSveltoTask> where TSveltoTask : ISveltoTask
    {
        internal class Process<TFlowModifier> : IProcessSveltoTasks<TSveltoTask> where TFlowModifier : IFlowModifier
        {
            public override string ToString()
            {
                return _runnerName;
            }

            public Process(FlushingOperation flushingOperation, TFlowModifier info, uint size, string runnerName)
            {
                _newTaskRoutines   = new ConcurrentQueue<TSveltoTask>();
                _runningCoroutines = new FasterList<TombstoneHandle>(size);
                _spawnedCoroutines = new TombstoneList<(TSveltoTask task, TombstoneHandle)>(size);
                _flushingOperation = flushingOperation;
                _info              = info;
                _runnerName        = $"{typeof(TFlowModifier).Name} - {runnerName} runner";
            }

            public bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
                where PlatformProfiler : IPlatformProfiler
            {
                DBC.Tasks.Check.Require(_flushingOperation.paused == false || _flushingOperation.kill == false
                  , $"cannot be found in pause state if killing has been initiated {_runnerName}");
                DBC.Tasks.Check.Require(_flushingOperation.kill == false || _flushingOperation.stopping == true
                  , $"if a runner is killed, must be stopped {_runnerName}");

                if (_flushingOperation.reset)
                {
                    foreach (ref var tr in _spawnedCoroutines)
                        tr.task.Dispose();  

                    while (_newTaskRoutines.TryDequeue(out TSveltoTask task))
                        task.Dispose();

                    _runningCoroutines.Clear();
                    _spawnedCoroutines.Clear();
                    _newTaskRoutines.Clear();
                    
                    _flushingOperation.Unstop();
                    
                    return false;
                }

                //a stopped runner can restart, and the design allows queueing new tasks in the stopped state,
                //although they won't be processed. In this sense, it's similar to paused. For this reason
                //_newTaskRoutines cannot be cleared in paused and stopped state.
                //This is done before the stopping check because all the tasks queued before stop will be stopped

                if (_newTaskRoutines.Count > 0 && _flushingOperation.acceptsNewTasks == true)
                {
                    while (_newTaskRoutines.TryPeek(out TSveltoTask task))
                    {
                        //only root tasks are added at this point
                        TombstoneHandle index = _spawnedCoroutines.Add((task, TombstoneHandle.Invalid));
                        _runningCoroutines.Add(index);
                        _newTaskRoutines.TryDequeue(out _); //Peek + dequeue to avoid race conditions when MT runner is used and the number of tasks is queried
#if DEBUG_TASKS_FLOW                        
                        Svelto.Console.Log($"spawn root task {_spawnedCoroutines[index].task} at location {_runningCoroutines.count - 1}");
#endif
                    }
                }

                //the difference between stop and pause is that pause freezes the task states, while stop flushes
                //them until there is nothing to run. Ever looping tasks are forced to be stopped and therefore
                //can terminate naturally
                if (_flushingOperation.stopping == true)
                {
                    //remember: it is not possible to clear new tasks after a runner is stopped, because a runner
                    //doesn't react immediately to a stop, so new valid tasks after the stop may be queued meanwhile.
                    //A Flush should be the safe way to be sure that only the tasks in process up to the Stop()
                    //point are stopped.
                    if (_runningCoroutines.count == 0 && _flushingOperation.kill == false)
                            //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.Unstop();
                }

                if (numberOfRunningTasks == 0 || (_flushingOperation.paused == true && _flushingOperation.stopping == false))
                    return true;

#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_runnerName);
#endif
                _info.Reset();

                if (_runningCoroutines.count > 0)
                {
                    int index = 0;

                    bool mustExit;
                    do
                    {
                        if (_info.CanProcessThis(ref index) == false)
                            break;

                        StepState result;

                        TombstoneHandle currentSpawnedTaskToRunIndex = _runningCoroutines[index]; //current position in _spawnedCoroutines
                        ref (TSveltoTask task, TombstoneHandle parentSpawnedTaskIndex) spawnedCoroutine =
                                ref _spawnedCoroutines[currentSpawnedTaskToRunIndex];
                        
                        //ATTENTION, this must be considered readonly. It is not marked as such because we use it to call stop.
                        //However Step CAN modify _spawnedCoroutines, making this ref invalid. So be careful when using it.
                        ref TSveltoTask currentSpawnedTaskToRun = ref spawnedCoroutine.task;
                        var spawnedCoroutineParentTaskIndex = spawnedCoroutine.parentSpawnedTaskIndex;

                        if (_flushingOperation.stopping)
                            currentSpawnedTaskToRun.Stop(); //the next step() will always complete the task and _continuations will be returned to the pool

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(currentSpawnedTaskToRun.name))
#endif

#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref currentSpawnedTaskToRun, _runnerName, (index, currentSpawnedTaskToRunIndex));
#else

                                result = currentSpawnedTaskToRun.Step(index,
                                    currentSpawnedTaskToRunIndex); //Note this can change _runningCoroutines when a child task is spawned
#endif
                        }
                        catch (Exception e)
                        {
                            Console.LogException(e, $"catching exception for root task {currentSpawnedTaskToRun.name}");

                            result = StepState.Faulted;
                        }

                        if (result != StepState.Faulted && _runningCoroutines[(int)index] != currentSpawnedTaskToRunIndex)
                        {
                            DBC.Tasks.Check.Require(result != StepState.Completed,
                                "a task cannot be completed and spawn a new task in the same step");

                            //if the task spawned a new task, the current task must be reprocessed
                        }
                        else 
                        if (result == StepState.Completed || result == StepState.Faulted)
                        {
                            try
                            {
                                //ATTENTION: cannot use ref here because Step can modify _spawnedCoroutines making the ref invalid
                                _spawnedCoroutines[currentSpawnedTaskToRunIndex].task.Dispose();
                            }
                            catch (Exception e)
                            {
                                Console.LogException(e, $"catching exception while disposing task {currentSpawnedTaskToRun.name}");
                            }
                            _spawnedCoroutines.RemoveAt(currentSpawnedTaskToRunIndex);
                            if (spawnedCoroutineParentTaskIndex.IsInvalid)
                            {
                                _runningCoroutines.UnorderedRemoveAt((uint)index); //the current task was a root task, remove it
#if DEBUG_TASKS_FLOW
                                    Svelto.Console.Log($"remove task {_spawnedCoroutines[currentSpawnedTaskToRunIndex].task} killed task in location {index}");
#endif

                            }
                            else
                            {
                                _runningCoroutines[index] = spawnedCoroutineParentTaskIndex; //the current task is finished, return to the parent one, however this index will be processed the next step
#if DEBUG_TASKS_FLOW
                                    Svelto.Console.Log($"remove task {_spawnedCoroutines[currentSpawnedTaskToRunIndex].task} replaced location {index} with task {_spawnedCoroutines[spawnedCoroutineParentTaskIndex].task}");
#endif
                            }
                        }
                        else
                            index++;

                        var hasCoroutineCompleted = (result & (StepState.Completed | StepState.Faulted)) != 0;

                        //ATTENTION: CanMoveNext must be evaluated BEFORE checking if the index is out of bound.
                        //CanMoveNext is allowed to change index: TimeSlicedFlow uses it to wrap the index back
                        //to 0 when the end of the list is reached without the time slice being exhausted, so
                        //that all the tasks are revisited several times in the same iteration until the budget
                        //expires. If the out-of-bound check ran first, short-circuit evaluation would make the
                        //wrap unreachable and TimeSlicedFlow would degrade to a plain TimeBoundFlow.
                        mustExit = 
                                _runningCoroutines.count                                                                   == 0
                             || _info.CanMoveNext<TSveltoTask>(ref index, _runningCoroutines.count, hasCoroutineCompleted) == false
                             || index                                                                                      >= _runningCoroutines.count;
                            
                    } while (mustExit == false);
                }

                return true;
            }

            /// <summary>
            /// Note: Svelto.Tasks 2.0 is based on the way SveltoTaskRunner works. However LeanTasks are quite heavy to iterate
            /// At the moment of writing they take up to 104 bytes (for iterator as a class). This can be reduced, but it must be reduced
            /// to 64bytes to be efficient. On trick could be to move as much data as possible inside the continuator class as long as the
            /// data is not needed to be accessed every step (ideally)
            /// Remember TSveltoTask can be also ExtraLean which are much smaller
            /// StartTask might be called from a different thread than the runner, that's why we need _newTaskRoutines as ConcurrentQueue
            /// </summary>
            /// <param name="task"></param>
            /// <param name="parentTaskIndex"></param>
            /// <param name="index"></param>
            public void AddTask(  in TSveltoTask task, (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) parentTaskIndex)
            {
                DBC.Tasks.Check.Require(_flushingOperation.kill == false,
                    $"can't schedule new routines on a killed scheduler {_runnerName}");

                if (parentTaskIndex.parentSpawnedTaskIndex == TombstoneHandle.Invalid)
                {
                    _newTaskRoutines.Enqueue(task); //root task
                }
                else
                {
                    //child task
                    var index = _spawnedCoroutines.Add((task, parentTaskIndex.parentSpawnedTaskIndex)); //must remember the parent task index in spawnedCoroutines
#if DEBUG_TASKS_FLOW                    
                    Svelto.Console.Log($"spawn task {_spawnedCoroutines[index]} in place of task {_spawnedCoroutines[parentTaskIndex.parentSpawnedTaskIndex]} at location {parentTaskIndex.runningTaskIndexToReplace}");
#endif
                    _runningCoroutines[(int)parentTaskIndex.runningTaskIndexToReplace] = index;
                }
            }

            //these are tasks that are not running yet, but are queued to be run
            readonly ConcurrentQueue<TSveltoTask> _newTaskRoutines;
            //these are just the running tasks, not all the spawned tasks. Only the leaves of spawned tasks run. RunningCoroutines contain the index into _spawnedCoroutines of the running task
            readonly FasterList<TombstoneHandle>      _runningCoroutines;
            //spawnedCoroutines holds all the spawned tasks. A new task can be spawned from a running task
            readonly TombstoneList<(TSveltoTask task, TombstoneHandle parentTaskindex)>   _spawnedCoroutines;
            readonly FlushingOperation      _flushingOperation;

            TFlowModifier _info;
            string _runnerName;

            public uint numberOfRunningTasks => (uint)_runningCoroutines.count;
            public uint numberOfQueuedTasks => (uint)_newTaskRoutines.Count;
            public uint numberOfTasks => (uint)_runningCoroutines.count + (uint)_newTaskRoutines.Count;
        }

        public class FlushingOperation
        {
            //simply pause the runner
            public bool paused          => (Volatile.Read(ref _state) & (int)StateFlags.Paused) != 0;
            //stop the current running tasks, but not the newly queued ones
            public bool stopping        => (Volatile.Read(ref _state) & (int)StateFlags.Stopped) != 0; //will be set to false in Unstop()
            //reset everything, the runner cannot be reused
            public bool kill            => (Volatile.Read(ref _state) & (int)StateFlags.Killed) != 0;
            //reset everything, the runner can be reused
            public bool reset           => (Volatile.Read(ref _state) & (int)StateFlags.Reset) != 0;   //will be set to false in Unstop()
            public bool acceptsNewTasks => paused == false && stopping == false && kill == false;

            public void Stop(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot stop a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way This must be bitmask
                var current = Volatile.Read(ref _state);
                var next    = (current | (int)StateFlags.Stopped) & ~(int)StateFlags.Paused;
                Volatile.Write(ref _state, next);
            }

            public void StopAndReset(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot flush a runner that is killed {name}");
                
                var current = Volatile.Read(ref _state);
                var next    = (current | (int)(StateFlags.Reset | StateFlags.Stopped)) & ~(int)StateFlags.Paused;
                Volatile.Write(ref _state, next);
            }

            public void Kill(string name)
            {
                if (kill == true)
                    return;

                //Atomic transition so other threads can never observe kill==true with stopping==false
                var current = Volatile.Read(ref _state);
                var next    = (current | (int)(StateFlags.Reset | StateFlags.Stopped | StateFlags.Killed)) & ~(int)StateFlags.Paused;
                Volatile.Write(ref _state, next);
            }

            public void Pause(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot pause a runner that is killed {name}");

                var current = Volatile.Read(ref _state);
                var next    = current | (int)StateFlags.Paused;
                Volatile.Write(ref _state, next);
            }

            public void Resume(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot resume a runner that is killed {name}");

                var current = Volatile.Read(ref _state);
                var next    = current & ~(int)StateFlags.Paused;
                Volatile.Write(ref _state, next);
            }

            internal void Unstop()
            {
                var current = Volatile.Read(ref _state);
                var next    = current & ~((int)StateFlags.Reset | (int)StateFlags.Stopped);
                Volatile.Write(ref _state, next);
            }

            [Flags]
            enum StateFlags
            {
                None   = 0,
                Paused = 1 << 0,
                Stopped= 1 << 1,
                Killed = 1 << 2,
                Reset  = 1 << 3
            }

            int _state;
        }
    }
}

