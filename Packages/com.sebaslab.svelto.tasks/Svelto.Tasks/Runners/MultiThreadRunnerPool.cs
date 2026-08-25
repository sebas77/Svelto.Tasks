using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.DataStructures;

namespace Svelto.Tasks.ExtraLean
{
    public class MultiThreadRunnerPoolException : Exception
    {
        public MultiThreadRunnerPoolException(string message) : base(message) { }
    }

    /// <summary>
    /// A pool of independent ExtraLean MultiThreadRunners. Each scheduled task is a root task dispatched
    /// round-robin to one of the inner runners. Runner-local continuation indices cannot be transferred
    /// between different inner runners, so this pool is intended for independently scheduled root tasks.
    /// The owner must guarantee that no AddTask or Stop calls overlap disposal.
    /// </summary>
    public sealed class MultiThreadRunnerPool : IRunner<ExtraLeanSveltoTask<IEnumerator>>, IDisposable
    {
        public int numberOfRunners => _runners.Length;
        public bool isDisposed => Volatile.Read(ref _disposed) == 1;
        public bool isStarted
        {
            get
            {
                for (int i = 0; i < _runners.Length; i++)
                {
                    if (_runners[i].isStarted == false)
                        return false;
                }

                return true;
            }
        }

        public MultiThreadRunnerPool(string name, int threadCount)
        {
            if (threadCount <= 0)
                throw new MultiThreadRunnerPoolException("threadCount must be greater than zero");

            _runners = new MultiThreadRunner[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                _runners[i] = new MultiThreadRunner(name + " #" + i, false, true);
                _runners[i].Resume();
            }
        }

        public MultiThreadRunnerPool(string name)
            : this(name, Math.Max(1, Environment.ProcessorCount - 2))
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTask(in ExtraLeanSveltoTask<IEnumerator> task,
            (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
        {
            if (index.parentSpawnedTaskIndex != TombstoneHandle.Invalid)
                throw new MultiThreadRunnerPoolException("MultiThreadRunnerPool accepts root tasks only");

            if (isDisposed)
                throw new MultiThreadRunnerPoolException("Cannot schedule tasks on a disposed MultiThreadRunnerPool");

            int next = Interlocked.Increment(ref _next);
            int runnerIndex = (int)((uint)next % (uint)_runners.Length);

            _runners[runnerIndex].AddTask(task, index);
        }

        /// <summary>
        /// Requests a graceful stop from every inner runner. Safe to call from a worker thread.
        /// Tasks added while stopping remain queued and can run after the inner runner unstops.
        /// </summary>
        public void Stop()
        {
            if (isDisposed)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Stop();
        }

        /// <summary>
        /// Disposes every inner runner, cleaning up running and queued tasks and waiting for
        /// worker shutdown. The owner must ensure that the pool is no longer being used.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Dispose();
        }

        readonly MultiThreadRunner[] _runners;
        int _next = -1;
        int _disposed;
    }

    /// <summary>
    /// Typed variant of MultiThreadRunnerPool for allocation-free ExtraLean struct root tasks.
    /// Tasks can add more root tasks while the pool is running.
    /// </summary>
    public sealed class MultiThreadRunnerPool<TTask> :
        IRunner<Struct.ExtraLeanSveltoTask<TTask>>, IDisposable where TTask : struct, IEnumerator, IDisposable
    {
        public int numberOfRunners => _runners.Length;
        public bool isDisposed => Volatile.Read(ref _disposed) == 1;

        public MultiThreadRunnerPool(string name, int threadCount, bool tightTasks = false)
        {
            if (threadCount <= 0)
                throw new MultiThreadRunnerPoolException("threadCount must be greater than zero");

            _runners = new Struct.MultiThreadRunner<TTask>[threadCount];
            for (int i = 0; i < threadCount; i++)
                _runners[i] = new Struct.MultiThreadRunner<TTask>(name + " #" + i, false, tightTasks);
        }

        public MultiThreadRunnerPool(string name, bool tightTasks = false)
            : this(name, Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTask(in Struct.ExtraLeanSveltoTask<TTask> task,
            (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
        {
            if (index.parentSpawnedTaskIndex != TombstoneHandle.Invalid)
                throw new MultiThreadRunnerPoolException("MultiThreadRunnerPool accepts root tasks only");

            if (isDisposed)
                throw new MultiThreadRunnerPoolException("Cannot schedule tasks on a disposed MultiThreadRunnerPool");

            int next = Interlocked.Increment(ref _next);
            int runnerIndex = (int)((uint)next % (uint)_runners.Length);
            _runners[runnerIndex].AddTask(task, index);
        }

        public void Stop()
        {
            if (isDisposed)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Stop();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            for (int i = 0; i < _runners.Length; i++)
                _runners[i].Dispose();
        }

        readonly Struct.MultiThreadRunner<TTask>[] _runners;
        int _next = -1;
        int _disposed;
    }
}
