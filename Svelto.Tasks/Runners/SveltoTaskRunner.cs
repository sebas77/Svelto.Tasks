using System;
using System.Threading;
using Svelto.Common;
using Svelto.Common.DataStructures;
using Svelto.DataStructures;


namespace Svelto.Tasks.Internal
{
    public static class SveltoTaskRunner<T> where T : ISveltoTask
    {
        internal class Process<TFlowModifier> : IProcessSveltoTasks where TFlowModifier : IFlowModifier
        {
            public override string ToString()
            {
                return _info.runnerName;
            }

            public Process
            (ThreadSafeQueue<T> newTaskRoutines, FasterList<T> coroutines, FasterList<T> spawnedCoroutines
              , FlushingOperation flushingOperation, TFlowModifier info)
            {
                DBC.Tasks.Check.Require(coroutines != null, "coroutine array cannot be null");
                DBC.Tasks.Check.Require(spawnedCoroutines != null, "spawnedCoroutines array cannot be null");
                DBC.Tasks.Check.Require(newTaskRoutines != null, "newTaskRoutines array cannot be null");

                _newTaskRoutines   = newTaskRoutines;
                _coroutines        = coroutines;
                _spawnedCoroutines = spawnedCoroutines;
                _flushingOperation = flushingOperation;
                _info              = info;
            }

            public bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
                where PlatformProfiler : IPlatformProfiler
            {
                DBC.Tasks.Check.Require(_flushingOperation.paused == false || _flushingOperation.kill == false
                  , $"cannot be found in pause state if killing has been initiated {_info.runnerName}");
                DBC.Tasks.Check.Require(_flushingOperation.kill == false || _flushingOperation.stopping == true
                  , $"if a runner is killed, must be stopped {_info.runnerName}");

                if (_flushingOperation.flush)
                {
                    _newTaskRoutines.Clear();
                }

                //a stopped runner can restart and the design allows to queue new tasks in the stopped state
                //although they won't be processed. In this sense it's similar to paused. For this reason
                //_newTaskRoutines cannot be cleared in paused and stopped state.
                //This is done before the stopping check because all the tasks queued before stop will be stopped
                if (_newTaskRoutines.count > 0 && _flushingOperation.acceptsNewTasks == true)
                {
                    _newTaskRoutines.DequeueAllInto(_coroutines);
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
                    if (_coroutines.count == 0)
                    {
                        if (_flushingOperation.kill == true)
                        {
                            //ContinuationEnumeratorInternal are intercepted by the finalizers and
                            //returned to the pool.`
                            _coroutines.Clear();
                            _newTaskRoutines.Clear();
                            return false;
                        }

                        //once all the coroutines are flushed the loop can return accepting new tasks
                        _flushingOperation.Unstop();
                    }
                }

                var coroutinesCount        = _coroutines.count;
                var spawnedCoroutinesCount = _spawnedCoroutines.count;

                if ((spawnedCoroutinesCount + coroutinesCount == 0)
                 || (_flushingOperation.paused == true && _flushingOperation.stopping == false))
                {
                    return true;
                }

#if TASKS_PROFILER_ENABLED
                Profiler.TaskProfiler.ResetDurations(_info.runnerName);
#endif
                _info.Reset();

                bool mustExit;

                if (spawnedCoroutinesCount > 0)
                {
                    var spawnedCoroutines = _spawnedCoroutines;
                    int index             = 0;

                    do
                    {
                        bool result;

                        ref var spawnedCoroutine = ref spawnedCoroutines[index];
                        
                        if (_flushingOperation.stopping)
                            spawnedCoroutine.Stop();

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(spawnedCoroutine.name))
#endif
#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref spawnedCoroutines[index], _info.runnerName);
#else

                            result = spawnedCoroutine.MoveNext();
#endif
                        }
                        catch
                        {
                            Svelto.Console.LogError(
                                $"catching exception for spawned task {spawnedCoroutine.name}");

                            throw;
                        }
                        
                        if (result == false)
                        {
                            _spawnedCoroutines.UnorderedRemoveAt((uint)index);

                            spawnedCoroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = (spawnedCoroutinesCount == 0 || index >= spawnedCoroutinesCount);
                    } while (!mustExit);
                }

                if (coroutinesCount > 0)
                {
                    int index = 0;

                    var coroutines = _coroutines.ToArrayFast(out _);

                    do
                    {
                        if (_info.CanProcessThis(ref index) == false)
                            break;

                        bool result;

                        ref var sveltoTask = ref coroutines[index];
                        
                        if (_flushingOperation.stopping)
                            sveltoTask.Stop();

                        try
                        {
#if ENABLE_PLATFORM_PROFILER
                            using (platformProfiler.Sample(sveltoTask.name))
#endif
                    
#if TASKS_PROFILER_ENABLED
                            result =
                                Profiler.TaskProfiler.MonitorUpdateDuration(ref coroutines[index], _info.runnerName);
#else
                            result = sveltoTask.MoveNext();
#endif
                        }
                        catch (Exception e)
                        {
                            Svelto.Console.LogException(e, $"catching exception for root task {sveltoTask.name}");

                            throw;
                        }

                        int previousIndex = index;

                        if (result == false)
                        {
                            DBC.Tasks.Check.Assert(_coroutines.count != 0, $"are you running a disposed runner? {this._info.runnerName}");
                            
                            _coroutines.UnorderedRemoveAt((uint)index);

                            coroutinesCount--;
                        }
                        else
                            index++;

                        mustExit = (coroutinesCount == 0
                         || _info.CanMoveNext(ref index, ref coroutines[previousIndex], coroutinesCount, !result)
                         == false || index >= coroutinesCount);
                    } while (!mustExit);
                }

                return true;
            }

            readonly ThreadSafeQueue<T> _newTaskRoutines;
            readonly FasterList<T>      _coroutines;
            readonly FasterList<T>      _spawnedCoroutines;
            readonly FlushingOperation  _flushingOperation;

            TFlowModifier _info;
        }

        //todo this must copy the SveltoTaskState pattern
        public class FlushingOperation
        {
            public bool paused          => Volatile.Read(ref _paused);
            public bool stopping        => Volatile.Read(ref _stopped);
            public bool kill            => Volatile.Read(ref _killed);
            public bool flush           => Volatile.Read(ref _flush);
            public bool acceptsNewTasks => paused == false && stopping == false && kill == false;

            public void Stop(string name)
            {
                DBC.Tasks.Check.Require(kill == false, $"cannot stop a runner that is killed {name}");

                //maybe I want both flags to be set in a thread safe way This must be bitmask
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

            public void StopAndFlush()
            {
                Volatile.Write(ref _flush, true);
                Volatile.Write(ref _stopped, true);
                Volatile.Write(ref _paused, false);
            }

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
                Volatile.Write(ref _stopped, false);
            }

            bool _paused;
            bool _stopped;
            bool _killed;
            bool _flush;
        }
    }
}