//#define DEBUG_TASKS_FLOW

using System;
using System.Collections.Concurrent;
using System.Threading;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.DataStructures.Experimental;

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
                _runningCoroutines = new FasterList<int>(size);
                _spawnedCoroutines = new TombstoneList<(TSveltoTask task, int)>(size);
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

                if (_flushingOperation.flush)
                    _newTaskRoutines.Clear();

                //a stopped runner can restart and the design allows queueing new tasks in the stopped state,
                //although they won't be processed. In this sense, it's similar to paused. For this reason
                //_newTaskRoutines cannot be cleared in paused and stopped state.
                //This is done before the stopping check because all the tasks queued before stop will be stopped
                else
                if (_newTaskRoutines.Count > 0 && _flushingOperation.acceptsNewTasks == true)
                {
                    while (_newTaskRoutines.TryDequeue(out TSveltoTask task))
                    {
                        //only root tasks are added at this point
                        int index = (int)_spawnedCoroutines.Add((task, -1));
                        _runningCoroutines.Add(index);
#if DEBUG_TASKS_FLOW                        
                        Svelto.Console.Log($"spawn root task {_spawnedCoroutines[index].task} at location {_runningCoroutines.count - 1}");
#endif
                    }
                }

                //the difference between stop and pause is that pause freeze the tasks states, while stop flush
                //them until there is nothing to run. Ever looping tasks are forced to be stopped and therefore
                //can terminate naturally
                if (_flushingOperation.stopping == true)
                {
                    //remember: it is not possible to clear new tasks after a runner is stopped, because a runner
                    //doesn't react immediately to a stop, so new valid tasks after the stop may be queued meanwhile.
                    //A Flush should be the safe way to be sure that only the tasks in process up to the Stop()
                    //point are stopped.
                    if (_runningCoroutines.count == 0)
                    {
                        if (_flushingOperation.kill == true)
                        {
                            //ContinuationEnumeratorInternal are intercepted by the finalizers and
                            //returned to the pool.`
                            _runningCoroutines.Clear();
                            _newTaskRoutines.Clear();
                            return false;
                        }

                        //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.Unstop();
                    }
                }

                if (numberOfRunningTasks == 0 || (_flushingOperation.paused == true && _flushingOperation.stopping == false))
                {
                    return true;
                }

#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_runnerName);
#endif
                _info.Reset();

                //these are the main coroutines
                if (_runningCoroutines.count > 0)
                {
                    int index = 0;

                    bool mustExit;
                    do 
                    {
                        if (_info.CanProcessThis(ref index) == false)
                            break;

                        StepState result;

                        var currentSpawnedTaskToRunIndex = _runningCoroutines[index]; //current position in _spawnedCoroutines
                        ref (TSveltoTask task, int parentSpawnedTaskIndex) spawnedCoroutine = ref _spawnedCoroutines[currentSpawnedTaskToRunIndex];
                        ref TSveltoTask currentSpawnedTaskToRun = ref spawnedCoroutine.task;
                        var spawnedCoroutineParentTaskIndex = spawnedCoroutine.parentSpawnedTaskIndex;
                        
                        if (_flushingOperation.stopping)
                            currentSpawnedTaskToRun.Stop();

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(currentSpawnedTaskToRun.name))
#endif
                    
#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref currentSpawnedTaskToRun, _runnerName, (index, currentSpawnedTaskToRunIndex));
#else

                            result = currentSpawnedTaskToRun.Step(index, currentSpawnedTaskToRunIndex); //Note this can change _runningCoroutines when a child task is spawned
#endif
                        }
                        catch (Exception e)
                        {
                            Svelto.Console.LogException(e, $"catching exception for root task {currentSpawnedTaskToRun.name}");

                            result = StepState.Faulted; //todo: in future it could be faulted if makes sense
                        }
                        
                        if (result != StepState.Faulted && _runningCoroutines[index] != currentSpawnedTaskToRunIndex)
                        {
                            DBC.Tasks.Check.Require(result != StepState.Completed, "a task cannot be completed and spawn a new task in the same step");
                            
                            //if the task spawned a new task, the current task must be reprocessed
                        }
                        else
                        if (result == StepState.Completed || result == StepState.Faulted)
                        {
                            _spawnedCoroutines.RemoveAt(currentSpawnedTaskToRunIndex);
                            if (spawnedCoroutineParentTaskIndex == -1)
                            {
                                _runningCoroutines.UnorderedRemoveAt((uint)index); //the current task was a root task, remove it
#if DEBUG_TASKS_FLOW                                
                                Svelto.Console.Log($"remove task {_spawnedCoroutines[currentSpawnedTaskToRunIndex].task} killed task in location {index}");
#endif

                            }
                            else
                            {
                                _runningCoroutines[index] = spawnedCoroutineParentTaskIndex; //the current task is finished, return to the parent one
#if DEBUG_TASKS_FLOW                          
                                Svelto.Console.Log($"remove task {_spawnedCoroutines[currentSpawnedTaskToRunIndex].task} replaced location {index} with task {_spawnedCoroutines[spawnedCoroutineParentTaskIndex].task}");
#endif
                            }
                        }
                        else
                            index++;

                        var hasCoroutineCompleted = (result & (StepState.Completed | StepState.Faulted)) != 0;
                        
                        mustExit = _runningCoroutines.count == 0 
                             || index >= _runningCoroutines.count
                             || _info.CanMoveNext<TSveltoTask>(ref index, _runningCoroutines.count, hasCoroutineCompleted) == false;
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
            public void AddTask(  in TSveltoTask task, (int runningTaskIndexToReplace, int parentSpawnedTaskIndex) parentTaskIndex)
            {
                DBC.Tasks.Check.Require(_flushingOperation.kill == false,
                    $"can't schedule new routines on a killed scheduler {_runnerName}");

                if (parentTaskIndex.parentSpawnedTaskIndex == -1)
                {
                    _newTaskRoutines.Enqueue(task); //root task
                }
                else
                {
                    //child task
                    int index = (int)_spawnedCoroutines.Add((task, parentTaskIndex.parentSpawnedTaskIndex)); //must remember the parent task index in spawnedCoroutines
#if DEBUG_TASKS_FLOW                    
                    Svelto.Console.Log($"spawn task {_spawnedCoroutines[index]} in place of task {_spawnedCoroutines[parentTaskIndex.parentSpawnedTaskIndex]} at location {parentTaskIndex.runningTaskIndexToReplace}");
#endif
                    _runningCoroutines[parentTaskIndex.runningTaskIndexToReplace] = index;
                }
            }

            //The only reason why spawnedCoroutine exist is to make FlowControl works. The most crucial is Serial. I can guarantee that the
            //tasks added in the runner by the user are executed in serial as long as the spawned tasks are executed separately, otherwise
            //I couldn't guarantee the order of the tasks executed one by one
            readonly ConcurrentQueue<TSveltoTask> _newTaskRoutines;
            readonly FasterList<int>      _runningCoroutines;
            readonly TombstoneList<(TSveltoTask task, int parentTaskindex)>   _spawnedCoroutines;
            readonly FlushingOperation      _flushingOperation;

            TFlowModifier _info;
            string _runnerName;

            public uint numberOfRunningTasks => (uint)_runningCoroutines.count;
            public uint numberOfQueuedTasks => (uint)_newTaskRoutines.Count;
            public uint numberOfTasks => (uint)_runningCoroutines.count + (uint)_newTaskRoutines.Count;
        }

        //todo this must copy the SveltoTaskState pattern
        public class FlushingOperation
        {
            public bool paused          => Volatile.Read(ref _paused);
            public bool stopping        => Volatile.Read(ref _stopped);
            public bool kill            => Volatile.Read(ref _killed);
            public bool flush           => Volatile.Read(ref _flush);
            public bool acceptsNewTasks => paused == false && stopping == false && kill == false;

            //todo: unit test. Test if the runner can be reused after this
            public void Stop(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot stop a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way This must be bitmask
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            //todo: unit test. Test if the runner can be reused after this
            public void StopAndFlush()
            {
                Volatile.Write(ref _flush, true);
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            //todo: unit test. Test if the runner can be reused after this
            public void Kill(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot kill a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way, meaning that the
                //flags must all be set at once. This must be bitmask
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _killed, true);
                Volatile.Write(ref _paused, false);
            }

            public void Pause(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot pause a runner that is killed {name}");

                Volatile.Write(ref _paused, true);
            }

            public void Resume(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot resume a runner that is killed {name}");

                Volatile.Write(ref _paused, false);
            }

            internal void Unstop()
            {
                Volatile.Write(ref _flush, false);
                Volatile.Write(ref _stopped, false);
            }

            bool _paused;
            bool _stopped;
            bool _killed;
            bool _flush;
        }
    }
}