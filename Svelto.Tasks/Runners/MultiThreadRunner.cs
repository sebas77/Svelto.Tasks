using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Svelto.Common;
using Svelto.Common.DataStructures;
using Svelto.DataStructures;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Internal;
using Svelto.Utilities;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    namespace Lean
    {
        public sealed class MultiThreadRunner : MultiThreadRunner<IEnumerator<TaskContract>>
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed,
                tightTasks)
            {
            }

            public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
            }
        }

        public class MultiThreadRunner<T> : Svelto.Tasks.MultiThreadRunner<LeanSveltoTask<T>>
            where T : IEnumerator<TaskContract>
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed,
                tightTasks)
            {
            }

            public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
            }
        }
    }

    namespace ExtraLean
    {
        public sealed class MultiThreadRunner : MultiThreadRunner<IEnumerator>
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed,
                tightTasks)
            {
            }

            public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
            }
        }

        public class MultiThreadRunner<T> : Svelto.Tasks.MultiThreadRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed,
                tightTasks)
            {
            }

            public MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
            }
        }
    }

    public class MultiThreadRunner<TTask> : MultiThreadRunner<TTask, StandardFlow> where TTask : ISveltoTask
    {
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name,
            new StandardFlow(), relaxed, tightTasks)
        {
        }

        public MultiThreadRunner(string name, float intervalInMs) : base(name, new StandardFlow(), intervalInMs)
        {
        }
    }

    /// <summary>
    /// The multithread runner always uses just one thread to run all the couroutines
    /// If you want to use a separate thread, you will need to create another MultiThreadRunner 
    /// </summary>
    /// <typeparam name="TTask"></typeparam>
    /// <typeparam name="TFlowModifier"></typeparam>
    public class MultiThreadRunner<TTask, TFlowModifier> : IRunner<TTask> where TTask : ISveltoTask
        where TFlowModifier : IFlowModifier
    {
        /// <summary>
        /// when the thread must run very tight and cache friendly tasks that won't allow the CPU to start new threads,
        /// passing the tightTasks as true would force the thread to yield every so often. Relaxed to true
        /// would let the runner be less reactive on new tasks added.  
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tightTasks"></param>
        public MultiThreadRunner(string name, TFlowModifier modifier, bool relaxed = false, bool tightTasks = false)
        {
            var runnerData = new RunnerData(relaxed, 0, name, tightTasks, modifier);

            Init(runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        public MultiThreadRunner(string name, TFlowModifier modifier, float intervalInMs)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false, modifier);

            Init(runnerData);
        }

        ~MultiThreadRunner()
        {
            Console.LogWarning("MultiThreadRunner has been garbage collected, this could have serious" +
                "consequences, are you sure you want this? ".FastConcat(_runnerData.name));

            Dispose();
        }

        public bool isStopping => _runnerData.waitForStop;

        public bool   isKilled                => _runnerData == null;
        public bool   isPaused                => _runnerData.isPaused;
        public string name                    => _runnerData.name;
        public uint   numberOfQueuedTasks     => _runnerData.numberOfQueuedTasks;
        public uint   numberOfRunningTasks    => _runnerData.numberOfRunningTasks;
        public uint   numberOfProcessingTasks => numberOfRunningTasks + numberOfQueuedTasks;
        public bool   hasTasks                => numberOfProcessingTasks != 0;

        public override string ToString()
        {
            return _runnerData.name;
        }

        public void Pause()
        {
            _runnerData.isPaused = true;
        }

        public void Resume()
        {
            _runnerData.isPaused = false;
        }

        public void Flush()
        {
            _runnerData.StopAndFlush();
        }

        public void Dispose()
        {
            if (isKilled == false)
                Kill(null);

            GC.SuppressFinalize(this);
        }

        public void StartTask(in TTask task)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");

            _runnerData.EnqueueNewTask(task);
        }

        public void SpawnContinuingTask(TTask task)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");

            _runnerData.EnqueueContinuingTask(task);
        }

        public void Stop()
        {
            if (isKilled == true)
                return;

            _runnerData.Stop();
        }

        public void Kill(Action onThreadKilled)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to kill an already killed runner");

            _runnerData.Kill(onThreadKilled);
            _runnerData = null;
        }

        public void Kill()
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to kill an already killed runner");

            _runnerData.Kill(null);
            _runnerData = null;
        }

        void Init(RunnerData runnerData)
        {
            _runnerData = runnerData;
#if !NETFX_CORE
            //threadpool doesn't work well with Unity apparently it seems to choke when too meany threads are started
            new Thread(runnerData.RunCoroutineFiber)
            {
                IsBackground = true,
                Name         = _runnerData.name
            }.Start();
#else
            Task.Factory.StartNew(() => runnerData.RunCoroutineFiber(), TaskCreationOptions.LongRunning);
#endif
        }

        class RunnerData
        {
            public uint numberOfRunningTasks => (uint)_coroutines.count + (uint)_spawnedCoroutines.count;
            public uint numberOfQueuedTasks  => _newTaskRoutines.count;

            public RunnerData(bool relaxed, float intervalInMs, string name, bool isRunningTightTasks,
                TFlowModifier modifier)
            {
                _watchForInterval    = new Stopwatch();
                _watchForLocking     = new Stopwatch();
                _coroutines          = new FasterList<TTask>();
                _spawnedCoroutines   = new FasterList<TTask>();
                _newTaskRoutines     = new ThreadSafeQueue<TTask>();
                _intervalInTicks     = (long)(intervalInMs * 10000);
                this.name            = name;
                _isRunningTightTasks = isRunningTightTasks;
                _flushingOperation   = new SveltoTaskRunner<TTask>.FlushingOperation();
                modifier.runnerName  = name;

                _process = new SveltoTaskRunner<TTask>.Process<TFlowModifier>(_newTaskRoutines, _coroutines,
                    _spawnedCoroutines, _flushingOperation, modifier);

                if (relaxed)
                    _lockingMechanism = RelaxedLockingMechanism;
                else
                    _lockingMechanism = QuickLockingMechanism;
            }

            internal void Stop()
            {
                _flushingOperation.Stop(name);
                //unlocking thread as otherwise the stopping flag will never be reset
                UnlockThread();
            }

            internal void StopAndFlush()
            {
                _flushingOperation.StopAndFlush();

                //unlocking thread as otherwise the stopping flag will never be reset
                UnlockThread();
            }

            internal void Kill(Action onThreadKilled)
            {
                _flushingOperation.Kill(name);

                _onThreadKilled = onThreadKilled;

                UnlockThread();
            }

            internal void EnqueueNewTask(in TTask task)
            {
                _newTaskRoutines.Enqueue(task);

                UnlockThread();
            }

            public void EnqueueContinuingTask(in TTask task)
            {
                _spawnedCoroutines.Add(task);

                UnlockThread();
            }

            void UnlockThread()
            {
                Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Release);
            }

            internal void RunCoroutineFiber()
            {
                try
                {
                    while (true)
                    {
                        using (_profiler.Sample(name))
                        {
                            if (_intervalInTicks > 0)
                                _watchForInterval?.Restart();
                            
                            if (_process.MoveNext(_profiler) == false)
                                break;

                            //If the runner is not killed
                            if (_flushingOperation.stopping == false)
                            {
                                //if the runner is paused enable the locking mechanism
                                if (_flushingOperation.paused == true)
                                    _lockingMechanism();

                                //if there is an interval time between calls we need to wait for it
                                if (_intervalInTicks > 0)
                                    WaitForInterval();

                                //if there aren't task left we put the thread in pause
                                if (numberOfRunningTasks == 0)
                                {
                                    if (numberOfQueuedTasks == 0)
                                        _lockingMechanism();
                                    else 
                                    if (_isRunningTightTasks == false)
                                        ThreadUtility.Wait(ref _yieldingCount, 16);
                                }
                                else
                                {
                                    //if it's not running tight tasks, let's let the runner breath a bit
                                    //every now and then
                                    if (_isRunningTightTasks == false)
                                        ThreadUtility.Wait(ref _yieldingCount, 16);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    Kill(null);

                    //the process must always complete naturally, otherwise the continuators won't be released.
                    using (_profiler.Sample(name))
                    {
                        while (_process.MoveNext(_profiler) == true) ;
                    }

                    throw;
                }
                finally
                {
                    _onThreadKilled?.Invoke();
                }
            }

            internal bool isPaused
            {
                get => _flushingOperation.paused;
                set
                {
                    _flushingOperation.Pause(name);

                    if (value == false)
                        UnlockThread();
                }
            }

            internal bool waitForStop => _flushingOperation.stopping;

            /// <summary>
            /// More reacting pause/resuming system. It spins for a while before reverting to the relaxing locking
            /// TODO: I CHANGED THIS AND DIDN'T PROPERLY TESTED IT, MUST UNIT TESTED PROPERLY 
            /// </summary>
            void QuickLockingMechanism()
            {
                var quickIterations = 0;
                var frequency       = 128;

                Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Acquire);

                while (Volatile.Read(ref _quickThreadSpinning) == (int)QuckLockinSpinningState.Acquire &&
                       quickIterations < 4096)
                {
                    ThreadUtility.Wait(ref quickIterations, frequency);

                    if (waitForStop) //we need to flush the queue, so the thread cannot stop
                        return;
                }

                //After the spinning, just revert to the normal locking mechanism
                RelaxedLockingMechanism();
            }

            /// <summary>
            /// Resuming a manual even can take a long time, but allow the thread to be pause and the core to be used
            /// by other threads.
            /// For the future: I tried all the combinations with ManualResetEvent (to slow to resume)
            /// and ManualResetEventSlim (spinning too much). This is the best solution:
            /// DO NOT TOUCH THE NUMBERS, THEY ARE THE BEST BALANCE BETWEEN CPU OCCUPATION AND RESUME SPEED
            /// </summary>
            void RelaxedLockingMechanism()
            {
                var       quickIterations = 0;
                var       frequency       = 64;
                _watchForLocking.Restart();

                using (_profiler.Sample("locked"))
                {
                    while (Volatile.Read(ref _quickThreadSpinning) == (int)QuckLockinSpinningState.Acquire)
                    {
                        ThreadUtility.LongWait(ref quickIterations, _watchForLocking, frequency);
                    }
                }
            }

            void WaitForInterval()
            {
                var quickIterations = 0;
                var frequency       = 16;

                while (_watchForInterval.ElapsedTicks < _intervalInTicks)
                {
                    ThreadUtility.LongWait(ref quickIterations, _watchForLocking, frequency);

                    if (waitForStop == true)
                        return;
                }
            }

            internal readonly string name;

            readonly ThreadSafeQueue<TTask> _newTaskRoutines;
            readonly FasterList<TTask>      _coroutines;
            readonly FasterList<TTask>      _spawnedCoroutines;
            readonly long                   _intervalInTicks;
            readonly bool                   _isRunningTightTasks;
            readonly Action                 _lockingMechanism;
            PlatformProfilerMT              _profiler;
            
            Action             _onThreadKilled;
            readonly Stopwatch _watchForInterval;
            readonly Stopwatch _watchForLocking;
            int                _quickThreadSpinning;
            int                _yieldingCount;

            readonly SveltoTaskRunner<TTask>.FlushingOperation      _flushingOperation;
            readonly SveltoTaskRunner<TTask>.Process<TFlowModifier> _process;

            enum QuckLockinSpinningState
            {
                Release,
                Acquire
            }
        }

        RunnerData _runnerData;
    }

    public class MultiThreadRunnerException : Exception
    {
        public MultiThreadRunnerException(string message) : base(message)
        {
        }
    }
}