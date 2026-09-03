using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Common;
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
        public sealed class MultiThreadRunner : MultiThreadRunner<IEnumerator<TaskContract>>, IGenericLeanRunner
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false,
                uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, relaxed, tightTasks, initialNumberOfTasks) { }

            public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, intervalInMs, initialNumberOfTasks) { }
        }

        public class MultiThreadRunner<T> : Svelto.Tasks.MultiThreadRunner<LeanSveltoTask<T>>, IGenericLeanRunner<T>
                where T : IEnumerator<TaskContract>
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false,
                uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, relaxed, tightTasks, initialNumberOfTasks)
            {
                UseFlowModifier(new StandardFlow());
            }

            public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, intervalInMs, initialNumberOfTasks)
            {
                UseFlowModifier(new StandardFlow());
            }
        }
    }

    namespace ExtraLean
    {
        public sealed class MultiThreadRunner : MultiThreadRunner<IEnumerator>
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false,
                uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, relaxed, tightTasks, initialNumberOfTasks) { }

            public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, intervalInMs, initialNumberOfTasks) { }
        }

        namespace Struct
        {
            public class MultiThreadRunner<TTask> : Svelto.Tasks.MultiThreadRunner<ExtraLeanSveltoTask<TTask>> where TTask : struct, IEnumerator
            {
                public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false,
                    uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                        base(name, relaxed, tightTasks, initialNumberOfTasks)
                {
                    UseFlowModifier(new StandardFlow());
                }

                public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                        base(name, intervalInMs, initialNumberOfTasks)
                {
                    UseFlowModifier(new StandardFlow());
                }
            }
        }

        public class MultiThreadRunner<TTask> : Svelto.Tasks.MultiThreadRunner<ExtraLeanSveltoTask<TTask>> where TTask : class, IEnumerator
        {
            public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false,
                uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, relaxed, tightTasks, initialNumberOfTasks)
            {
                UseFlowModifier(new StandardFlow());
            }

            public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE) :
                    base(name, intervalInMs, initialNumberOfTasks)
            {
                UseFlowModifier(new StandardFlow());
            }
        }
    }

    /// <summary>
    /// The multithread runner always uses just one thread to run all the coroutines
    /// If you want to use a separate thread, you will need to create another MultiThreadRunner 
    /// </summary>
    /// <typeparam name="TTask"></typeparam>
    /// <typeparam name="TFlowModifier"></typeparam>
    public class MultiThreadRunner<TTask> : IRunner<TTask> where TTask : ISveltoTask
    {
        /// <summary>
        /// When the thread runs tight, cache-friendly tasks, passing tightTasks as true skips periodic waits
        /// while tasks are active and may consume a core more aggressively. Relaxed set to true makes the
        /// runner less reactive to newly added tasks.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tightTasks"></param>
        /// <param name="initialNumberOfTasks">
        /// initial capacity of the runner internal task containers. Size it to the expected
        /// number of concurrent tasks to avoid buffer growth allocations at runtime.
        /// </param>
        public MultiThreadRunner(string name, bool relaxed = false, bool tightTasks = false, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE)
        {
            var runnerData = new RunnerData(relaxed, 0, name, tightTasks, initialNumberOfTasks);

            Init(runnerData);
        }

        /// <summary>
        /// Start a Multithread runner that won't take 100% of the CPU
        /// </summary>
        /// <param name="name"></param>
        /// <param name="intervalInMs"></param>
        /// <param name="initialNumberOfTasks">
        /// initial capacity of the runner internal task containers. Size it to the expected
        /// number of concurrent tasks to avoid buffer growth allocations at runtime.
        /// </param>
        public MultiThreadRunner(string name, uint intervalInMs, uint initialNumberOfTasks = NUMBER_OF_INITIAL_COROUTINE)
        {
            var runnerData = new RunnerData(true, intervalInMs, name, false, initialNumberOfTasks);

            Init(runnerData);
        }

        protected const uint NUMBER_OF_INITIAL_COROUTINE = 3;

        ~MultiThreadRunner()
        {
            Console.LogWarning("MultiThreadRunner has been garbage collected, this could have serious" +
                "consequences, are you sure you want this? ".FastConcat(_runnerData.name));

            var runnerData = _runnerData;
            if (runnerData != null)
            {
                runnerData.Kill();
                _runnerData = null;
            }
        }

        public bool isStopping => _runnerData.waitForStop;
        public bool isValid => isKilled == false;

        public bool isStarted => _runnerData != null && Volatile.Read(ref _runnerData._isStarted) == 1;

        public bool   isKilled                => _runnerData == null;
        public bool   isPaused                => _runnerData.isPaused;
        public string name                    => _runnerData.name;
        public uint   numberOfQueuedTasks     => _runnerData.numberOfQueuedTasks;
        public uint   numberOfRunningTasks    => _runnerData.numberOfRunningTasks;
        public uint   numberOfProcessingTasks => _runnerData.numberOfTasks;
        public bool   hasTasks                => numberOfProcessingTasks != 0;

        public override string ToString()
        {
            return _runnerData.name;
        }

        /// <summary>
        /// Freezes running tasks without changing their state. Queued tasks wait until Resume is called.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pause()
        {
            _runnerData.isPaused = true;
            Volatile.Write(ref _runnerData._quickThreadSpinning, (int)RunnerData.QuckLockinSpinningState.Acquire);
        }

        /// <summary>
        /// Resumes tasks previously frozen by Pause.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resume()
        {
            _runnerData.isPaused = false;
        }

        /// <summary>
        /// Disposes running and queued tasks, blocks until reset completes, and leaves the worker reusable.
        /// Task submission is rejected while the reset is in progress.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _runnerData.StopAndFlush();
        }

        /// <summary>
        /// Disposes every task and terminates the worker. Returns only after the worker exits.
        /// </summary>
        public void Dispose()
        {
            if (isKilled == true)
            {
                //already terminated by a previous Dispose or by the finalizer: just disarm it
                GC.SuppressFinalize(this);
                return;
            }

            var runnerData = _runnerData;

            if (Thread.CurrentThread == runnerData.workerThread)
                throw new MultiThreadRunnerException("Cannot dispose a runner from its worker thread");

            //Suppression sits here on purpose, after the worker-thread guard:
            //- a rejected Dispose (misuse from the worker thread) must leave the finalizer armed,
            //  so a runner the caller failed to dispose is still cleaned up (and logged) at GC time
            //  instead of leaking silently with a live worker;
            //- once disposal is accepted, suppression must happen before Kill so the finalizer can
            //  never interleave with the teardown and Kill the runner concurrently (double-kill).
            GC.SuppressFinalize(this);

            runnerData.Kill();
            _runnerData = null;

            if (runnerData.workerThread.Join(LIFECYCLE_TIMEOUT_MS) == false)
                throw new MultiThreadRunnerException($"Runner {runnerData.name} did not terminate within the timeout");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTask(in TTask task,
            (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
        {
            var runnerData = _runnerData;
            if (runnerData == null)
                throw new MultiThreadRunnerException("Trying to start a task on a killed runner");

            runnerData.StartTask(task, index);
        }

        /// <summary>
        /// Cancels and disposes running tasks. Queued tasks wait and run after the runner automatically unstops.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stop()
        {
            if (isKilled == true)
                return;

            _runnerData.Stop();
        }

        void Init(RunnerData runnerData)
        {
            _runnerData = runnerData;

            Pause();

            runnerData.workerThread = new Thread(runnerData.RunCoroutineFiber)
            {
                IsBackground = true,
                Name         = _runnerData.name
            };
            runnerData.workerThread.Start();
        }

        public void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
        {
            _runnerData.UseFlowModifier(modifier);

            Resume();
        }

        /// <summary>
        /// Sets a callback invoked on the runner thread whenever the runner runs out of running
        /// tasks, right before the runner is parked by the locking mechanism. The callback can
        /// schedule new tasks on the runner to keep it spinning without parking it.
        /// </summary>
        public void SetIdleCallback(Action onIdleCallback)
        {
            _runnerData._idleCallback = onIdleCallback;
        }

        class RunnerData
        {
            public uint numberOfRunningTasks => _processor.numberOfRunningTasks;
            public uint numberOfQueuedTasks  => _processor.numberOfQueuedTasks;
            public uint numberOfTasks        => _processor.numberOfTasks;
            bool hasTasks                    => numberOfTasks != 0;

            public RunnerData(bool relaxed, uint intervalInMs, string name, bool isRunningTightTasks,
                uint initialNumberOfTasks)
            {
                _watchForInterval    = new Stopwatch();
                _watchForLocking     = new Stopwatch();
                _intervalInTicks     = TimeSpan.FromMilliseconds(intervalInMs).Ticks;
                this.name            = name;
                _isRunningTightTasks = isRunningTightTasks;
                _flushingOperation   = new SveltoTaskRunner<TTask>.FlushingOperation();
                _initialNumberOfTasks = initialNumberOfTasks;

                if (relaxed)
                    _lockingMechanism = RelaxedLockingMechanism;
                else
                    _lockingMechanism = QuickLockingMechanism;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Stop()
            {
                _flushingOperation.Stop(name);
                //unlocking thread as otherwise the stopping flag will never be reset
                UnlockThread();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void StopAndFlush()
            {
                if (Thread.CurrentThread == workerThread)
                    throw new MultiThreadRunnerException("Cannot flush a runner from its worker thread");

                lock (_taskAdmissionLock)
                    _flushingOperation.StopAndReset(name);

                //unlocking thread as otherwise the stopping flag will never be reset
                UnlockThread();

                var then = DateTime.UtcNow.AddMilliseconds(LIFECYCLE_TIMEOUT_MS);
                while (_flushingOperation.reset && DateTime.UtcNow < then)
                    ThreadUtility.TakeItEasy();

                if (_flushingOperation.reset)
                    throw new MultiThreadRunnerException($"Runner {name} did not flush within the timeout");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Kill()
            {
                lock (_taskAdmissionLock)
                    _flushingOperation.Kill(name);

                UnlockThread();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void StartTask(in TTask task, (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
            {
                // Serializing admission with reset/kill closes the check-then-enqueue race: a task is either
                // submitted before cleanup starts and included in it, or rejected until Flush completes.
                lock (_taskAdmissionLock)
                {
                    if (_flushingOperation.reset || _flushingOperation.kill)
                        throw new MultiThreadRunnerException($"Cannot schedule tasks while runner {name} is flushing or disposed");

                    var processor = _processor;
                    if (processor == null)
                    {
                        _queuedBeforeInit.Enqueue(task);
                        UnlockThread();
                        return;
                    }

                    processor.AddTask(task, index);
                }

                UnlockThread();
            }

            public void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
            {
                _processor = new SveltoTaskRunner<TTask>.Process<TFlowModifier>(_flushingOperation, modifier, _initialNumberOfTasks, name);

                while (_queuedBeforeInit.TryDequeue(out TTask task))
                    _processor.AddTask(task, (default, TombstoneHandle.Invalid));
            }

            internal void RunCoroutineFiber()
            {
                Volatile.Write(ref _isStarted, 1);

#if TASKS_PROFILER_ENABLED
                var profilerThreadDriver = Profiler.TaskProfiler.BeginWorkerThread(name);
                try
                {
#endif
                try
                {
                    //the loop body (including the using) lives in its own method on purpose:
                    //IL2CPP would otherwise emit two nested Finally blocks in one function,
                    //which crashes the MSVC compiler with C1001 (internal compiler error)
                    while (RunOneIteration()) { }
                }
                catch
                {
                    if (_flushingOperation.kill == false)
                        _flushingOperation.Kill(name);

                    _processor = null;

                    throw;
                }
#if TASKS_PROFILER_ENABLED
                }
                finally
                {
                    Profiler.TaskProfiler.EndWorkerThread(profilerThreadDriver);
                }
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool RunOneIteration()
            {
                using (_profiler.Sample(name))
                {
                    if (_intervalInTicks > 0)
                        _watchForInterval?.Restart();

                    //if the runner is paused enable the locking mechanism
                    if (_flushingOperation.paused == true && _flushingOperation.stopping == false)
                        _lockingMechanism();

                    if (_processor == null)
                        throw new MultiThreadRunnerException("No flow modifier has been set for the runner ".FastConcat(name));

                    if (_processor.MoveNext(_profiler) == false)
                        return _flushingOperation.kill == false;

                    //If the runner is not stopped
                    if (_flushingOperation.stopping == false)
                    {
                        //if there is an interval time between calls we need to wait for it
                        if (_intervalInTicks > 0)
                            WaitForInterval();

                        //before parking the thread, let the owner feed more tasks: the callback
                        //runs on this worker thread and can AddTask, avoiding the locking mechanism
                        if (numberOfRunningTasks == 0)
                        {
                            try
                            {
                                _idleCallback?.Invoke();
                            }
                            catch (Exception e)
                            {
                                //a throwing callback must not kill the worker thread
                                Console.LogError($"MultiThreadRunner {name} idle callback exception: {e}");
                            }

                            //re-check: the callback may have queued new tasks
                            if (numberOfRunningTasks == 0)
                            {
                                if (numberOfQueuedTasks == 0)
                                    _lockingMechanism();
                                else if (_isRunningTightTasks == false)
                                    ThreadUtility.Wait(ref _yieldingCount, 16);
                            }
                        }
                        else
                        {
                            //if it's not running tight tasks, let's let the runner breath a bit
                            //every so often
                            if (_isRunningTightTasks == false)
                                ThreadUtility.Wait(ref _yieldingCount, 16);
                        }
                    }

                    return true;
                }
            }

            internal bool isPaused
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _flushingOperation.paused;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (value)
                        _flushingOperation.Pause(name);
                    else
                        _flushingOperation.Resume(name);

                    if (value == false)
                        UnlockThread();
                }
            }

            internal bool waitForStop
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return _flushingOperation.stopping;
                }
            }

            /// <summary>
            /// More reacting pause/resuming system. It spins for a while before reverting to the relaxing locking
            /// _quickThreadSpinning is used as a lock-free synchronization primitive.
            /// Acquire: The thread is spinning/waiting.
            /// Release: The thread has been signaled to wake up (by AddTask, Resume, Stop, etc.).
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void QuickLockingMechanism()
            {
                var quickIterations = 0;
                var frequency       = 128;

                Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Acquire);

                if (waitForStop || (isPaused == false && hasTasks))
                {
                    Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Release);
                    return;
                }

                while (Volatile.Read(ref _quickThreadSpinning) == (int)QuckLockinSpinningState.Acquire &&
                       quickIterations                         < 4096)
                {
                    if (waitForStop || (isPaused == false && hasTasks)) //a task can be queued after entering the wait state
                        return;

                    ThreadUtility.Wait(ref quickIterations, frequency);
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void RelaxedLockingMechanism()
            {
                var       quickIterations = 0;
                var       frequency       = 64;

                Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Acquire);

                if (waitForStop || (isPaused == false && hasTasks))
                {
                    Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Release);

                    return;
                }

                _watchForLocking.Restart();

                while (Volatile.Read(ref _quickThreadSpinning) == (int)QuckLockinSpinningState.Acquire)
                {
                    if (waitForStop || (isPaused == false && hasTasks)) //a task can be queued after entering the wait state
                        return;

                    ThreadUtility.LongWait(ref quickIterations, _watchForLocking, frequency);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void WaitForInterval()
            {
                var quickIterations = 0;
                var frequency       = 16;

                while (_watchForInterval.Elapsed.Ticks < _intervalInTicks)
                {
                    ThreadUtility.LongWaitLeft(_intervalInTicks, ref quickIterations, _watchForLocking, frequency);

                    if (waitForStop == true)
                        return;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void UnlockThread()
            {
                Volatile.Write(ref _quickThreadSpinning, (int)QuckLockinSpinningState.Release);
            }

            internal readonly string name;

            readonly long                   _intervalInTicks;
            readonly bool                   _isRunningTightTasks;
            readonly uint                   _initialNumberOfTasks;
            readonly Action                 _lockingMechanism;
            PlatformProfilerMT              _profiler;

            //set through SetIdleCallback, invoked by the worker thread before parking
            internal volatile Action _idleCallback;

            readonly Stopwatch _watchForInterval;
            readonly Stopwatch _watchForLocking;
            readonly object _taskAdmissionLock = new object();

            /// <summary>
            /// _quickThreadSpinning is used as a lock-free synchronization primitive.
            /// Acquire: The thread is spinning/waiting.
            /// Release: The thread has been signaled to wake up (by AddTask, Resume, Stop, etc.).
            /// </summary>
            internal int _quickThreadSpinning;

            internal int _isStarted;
            internal Thread workerThread;

            internal enum QuckLockinSpinningState
            {
                Acquire = 0,
                Release = 1
            }

            int _yieldingCount;
            SveltoTaskRunner<TTask>.FlushingOperation _flushingOperation;
            IProcessSveltoTasks<TTask> _processor;

            readonly ConcurrentQueue<TTask> _queuedBeforeInit = new ConcurrentQueue<TTask>();
        }

        RunnerData _runnerData;

        const int LIFECYCLE_TIMEOUT_MS = 2000;
    }

    public class MultiThreadRunnerException : Exception
    {
        public MultiThreadRunnerException(string message) : base(message) { }
    }
}
