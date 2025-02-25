using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Common;
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
            protected MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed,
                tightTasks)
            {
                UseFlowModifier(new StandardFlow());
            }

            protected MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
                UseFlowModifier(new StandardFlow());
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

        public class MultiThreadRunner<TTask> : Svelto.Tasks.MultiThreadRunner<ExtraLeanSveltoTask<TTask>> where TTask : IEnumerator
        {
            protected MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false) : base(name, relaxed, tightTasks)
            {
                UseFlowModifier(new StandardFlow());
            }

            protected MultiThreadRunner(string name, float intervalInMs) : base(name, intervalInMs)
            {
                UseFlowModifier(new StandardFlow());
            }
        }
    }

    /// <summary>
    /// The multithread runner always uses just one thread to run all the couroutines
    /// If you want to use a separate thread, you will need to create another MultiThreadRunner 
    /// </summary>
    /// <typeparam name="TTask"></typeparam>
    /// <typeparam name="TFlowModifier"></typeparam>
    public class MultiThreadRunner<TTask> : IRunner<TTask> where TTask : ISveltoTask
    {
        /// <summary>
        /// when the thread must run very tight and cache friendly tasks that won't allow the CPU to start new threads,
        /// passing the tightTasks as true would force the thread to yield every so often. Relaxed to true
        /// would let the runner be less reactive on new tasks added.  
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tightTasks"></param>
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false)
        {
            var runnerData = new RunnerData(relaxed, 0, name, tightTasks);

            Init(runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        public MultiThreadRunner(string name, float intervalInMs)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false);

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

        public void AddTask( in TTask task, (int runningTaskIndexToReplace, int parentSpawnedTaskIndex) index)
        {
            if (isKilled == true)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");

            _runnerData.StartTask(task, index);
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

            new Thread(runnerData.RunCoroutineFiber)
            {
                IsBackground = true,
                Name         = _runnerData.name
            }.Start();
        }
        
        protected void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
        {
            _runnerData.UseFlowModifier(modifier);
        }

        class RunnerData
        {
            public uint numberOfRunningTasks => _processor.numberOfRunningTasks;
            public uint numberOfQueuedTasks  => _processor.numberOfQueuedTasks;

            public RunnerData(bool relaxed, float intervalInMs, string name, bool isRunningTightTasks)
            {
                _watchForInterval    = new Stopwatch();
                _watchForLocking     = new Stopwatch();
                _intervalInTicks     = (long)(intervalInMs * 10000);
                this.name            = name;
                _isRunningTightTasks = isRunningTightTasks;
                _flushingOperation   = new SveltoTaskRunner<TTask>.FlushingOperation();

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void StartTask(in TTask task, (int runningTaskIndexToReplace, int parentSpawnedTaskIndex) index)
            {
                _processor.AddTask(task, index);

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
                            
                            if (_processor.MoveNext(_profiler) == false)
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
                                    //every so often
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

                    _processor = null;

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
            
            public void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
            {
                _processor = new SveltoTaskRunner<TTask>.Process<TFlowModifier>(_flushingOperation, modifier, NUMBER_OF_INITIAL_COROUTINE, name);
            }

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
            /// Resuming a manual even can take a long time, but allow the thread to be paused and the core to be used
            /// by other threads.
            /// For the future: I tried all the combinations with ManualResetEvent (too slow to resume)
            /// and ManualResetEventSlim (spinning too much). This is the best solution:
            /// DO NOT TOUCH THE NUMBERS, THEY ARE THE BEST BALANCE BETWEEN CPU OCCUPATION AND RESUME SPEED
            /// </summary>
            void RelaxedLockingMechanism()
            {
                var       quickIterations = 0;
                var       frequency       = 64;
                _watchForLocking.Restart();

                while (Volatile.Read(ref _quickThreadSpinning) == (int)QuckLockinSpinningState.Acquire)
                {
                    ThreadUtility.LongWait(ref quickIterations, _watchForLocking, frequency);
                }
            }

            void WaitForInterval()
            {
                var quickIterations = 0;
                var frequency       = 16;

                while (_watchForInterval.ElapsedTicks < _intervalInTicks)
                {
                    ThreadUtility.LongWaitLeft(_intervalInTicks, ref quickIterations, _watchForLocking, frequency);

                    if (waitForStop == true)
                        return;
                }
            }

            internal readonly string name;

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
            IProcessSveltoTasks<TTask> _processor;

            enum QuckLockinSpinningState
            {
                Release,
                Acquire
            }

            const uint NUMBER_OF_INITIAL_COROUTINE = 3;
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