using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Common;
using Svelto.Tasks.ExtraLean;

namespace Svelto.Tasks.Parallelism
{
    public class MultiThreadedParallelTaskCollectionException : Exception
    {
        public MultiThreadedParallelTaskCollectionException(string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection) : base(
            canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection) { }
    }

    namespace ExtraLean
    {
        public interface IParallelTask: IEnumerator, IDisposable { }

        public abstract class BaseMultiThreadedParallelTaskCollection<TTask> where TTask : IParallelTask
        {
            public event Action onComplete;

            public bool isRunning { private set; get; }

            /// <summary>
            ///  
            /// </summary>
            /// <param name="name"></param>
            /// <param name="numberOfThreads"></param>
            /// <param name="tightTasks">
            /// if several cache friendly and optimized tasks run in parallel, using tightTasks may improve parallelism
            /// as gives the chance to other threads to run.
            /// </param>
            public BaseMultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
            {
                _decrementConcurrentOperationsCounterDelegate = DecrementConcurrentOperationsCounter;
                _runEnumerator = new RunEnumerator(this);
                DBC.Tasks.Check.Require(numberOfThreads > 0, "doesn't make much sense to use this with 0 threads");

                _name = name;

                InitializeThreadsAndData(numberOfThreads, tightTasks);
            }

            public BaseMultiThreadedParallelTaskCollection(string name, bool tightTasks): this(name,
                (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks) { }

            /// <summary>
            /// Add can be called by another thread, so if the collection is already running
            /// I can't allow adding more tasks.
            /// </summary>
            /// <param name="enumerator"></param>
            /// <exception cref="DBC.Tasks.PreconditionException">debug builds only</exception>
            public void Add(in TTask enumerator)
            {
                DBC.Tasks.Check.Require(isRunning == false,
                    "can't add tasks on a started MultiThreadedParallelTaskCollection");

                _parallelTasks.Add(enumerator);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                //isDisposed can be set by the GC finalizer thread
                if (Volatile.Read(ref _isDisposed))
                    return false;

                if (RunMultiThreadParallelTasks())
                    return true;

                // finished naturally: runners can be reused, so don't dispose them here
                isRunning = false;

                if (onComplete != null)
                    onComplete();

                return false;
            }

            /// <summary>
            /// Returns the reusable execution view of this collection. Completing or disposing this
            /// enumerator ends only the current run; call Dispose on the collection for final cleanup.
            /// The same view must not be run concurrently.
            /// </summary>
            public IEnumerator<TaskContract> Run()
            {
                return _runEnumerator;
            }

            public void Reset()
            {
                Stop(0);

                _parallelTasks.Clear();
                isRunning = false;
            }

            public void Stop()
            {
                Stop(0);
            }

            public void Stop(int msTimeout)
            {
                if (isRunning == false)
                    return;

                for (int i = 0; i < _runners.Length; i++)
                    _runners[i].Stop();

                //wait until each runner has finished flushing its running tasks
                for (int i = 0; i < _runners.Length; i++)
                    _runners[i].WaitForTasksDone(msTimeout);

                isRunning = false;
            }

            public void Dispose()
            {
                if (Volatile.Read(ref _isDisposed) == true)
                    return;

                Volatile.Write(ref _isDisposed, true);
                WaitForTaskSchedulingToFinish();

                // If tasks were never started, the only place they exist is _parallelTasks.
                // If tasks were started, their Dispose will be called by the runners.
                if (isRunning == false)
                {
                    for (int i = 0; i < _parallelTasks.Count; i++)
                        _parallelTasks[i].Dispose();
                }

                //Runners own the tasks that were successfully scheduled. Dispose the leftovers
                //still sitting in the feed queue (unclaimed, or handed back while stopping).
                while (_queuedTasks.TryDequeue(out TTask task))
                    task.Dispose();

                _parallelTasks.Clear();

                if (_runners != null)
                {
                    for (int i = 0; i < _runners.Length; i++)
                        _runners[i].Dispose();
                }

                _runners            = null;
                onComplete          = null;
                isRunning           = false;

                GC.SuppressFinalize(this);
            }

            public override string ToString()
            {
                return _name;
            }

            ~BaseMultiThreadedParallelTaskCollection()
            {
                Console.LogWarning($"MultiThreadedParallelTaskCollection {_name} wasn't disposed of correctly. You forgot to call Dispose()");

                Dispose();
            }

            void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
            {
                _runners = new Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>[numberOfThreads];

                //prepare a single multithread runner for each group of fiber like task collections
                //number of threads can be less than the number of tasks to run
                for (int i = 0; i < numberOfThreads; i++)
                {
                    int runnerIndex = i;
                    var runner = new Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>(
                        "MultiThreadedParallelRunner ".FastConcat(_name, " #").FastConcat(i), false, tightTasks);

                    runner.SetIdleCallback(() => FeedRunner(runnerIndex));
                    _runners[i] = runner;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool RunMultiThreadParallelTasks()
            {
                if (_isDisposed == true)
                    return false;

                if (isRunning == false)
                {
                    if (_parallelTasks.Count == 0)
                        return false;

                    isRunning = true;
                    Volatile.Write(ref _counter, _parallelTasks.Count);

                    //The list is the source of truth for re-runs; the queue is the thread-safe
                    //feed runners steal from. Destructive dequeues make claims race-free even at
                    //wave boundaries (a stray idle callback can claim an item once, never twice).
                    while (_queuedTasks.TryDequeue(out _)) { }

                    for (int i = 0; i < _parallelTasks.Count; i++)
                        _queuedTasks.Enqueue(_parallelTasks[i]);

                    //Schedule just one task per runner. When it runs out of work, its idle callback
                    //dequeues the next task, self-balancing tasks with uneven durations.
                    for (int i = 0; i < _runners.Length; i++)
                    {
                        if (TryScheduleNextTask(i) == false)
                            break;
                    }
                }

                //wait for completion, I am not using signaling as this Collection could be yielded by a main thread runner
                return Volatile.Read(ref _counter) > 0;
            }

            void FeedRunner(int runnerIndex)
            {
                //runs on the runner worker thread
                TryScheduleNextTask(runnerIndex);
            }

            bool TryScheduleNextTask(int runnerIndex)
            {
                Interlocked.Increment(ref _activeSchedulers);

                try
                {
                    if (Volatile.Read(ref _isDisposed))
                        return false;

                    if (_queuedTasks.TryDequeue(out TTask task) == false)
                        return false;

                    try
                    {
                        Wrap(task).RunOn(_runners[runnerIndex]);
                        return true;
                    }
                    catch (MultiThreadRunnerException)
                    {
                        //The runner is stopping/killed: give the task back. On a stopped collection it
                        //will simply be re-fed from the list on the next run; on a disposed one the
                        //drain in Dispose runs only after every in-flight claim has exited, so it is
                        //guaranteed to collect what was handed back here.
                        _queuedTasks.Enqueue(task);
                        return false;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSchedulers);
                }
            }

            void WaitForTaskSchedulingToFinish()
            {
                SpinWait spinner = new SpinWait();
                while (Volatile.Read(ref _activeSchedulers) != 0)
                    spinner.SpinOnce();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            WrapEnumerator Wrap(in TTask task)
            {
                return new WrapEnumerator(task, _decrementConcurrentOperationsCounterDelegate);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DecrementConcurrentOperationsCounter()
            {
                Interlocked.Decrement(ref _counter);
            }

            protected Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>[] _runners;
            readonly List<TTask>            _parallelTasks = new List<TTask>();
            readonly ConcurrentQueue<TTask> _queuedTasks   = new ConcurrentQueue<TTask>();

            int  _counter;
            int  _activeSchedulers;
            bool _isDisposed;

            readonly string _name;
            readonly Action _decrementConcurrentOperationsCounterDelegate;
            readonly RunEnumerator _runEnumerator;

            sealed class RunEnumerator : IEnumerator<TaskContract>
            {
                internal RunEnumerator(BaseMultiThreadedParallelTaskCollection<TTask> owner)
                {
                    _owner = owner;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => _owner.MoveNext();

                public void Reset() { }
                public void Dispose() { }

                public TaskContract Current
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get =>
                            TaskContract.Continue
                                   .It; //we don't want to yield anything, we just want to run the tasks in parallel and wait for them to finish synchronously
                }

                object IEnumerator.Current => Current;

                public override string ToString() => _owner.ToString();

                readonly BaseMultiThreadedParallelTaskCollection<TTask> _owner;
            }

            protected struct WrapEnumerator : IEnumerator, IDisposable
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public WrapEnumerator(in TTask task, Action decrementConcurrentOperationsCounter)
                {
                    _task = task;
                    _decrementConcurrentOperationsCounter = decrementConcurrentOperationsCounter;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => _task.MoveNext();

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Reset()    => _task.Reset();

                public object Current
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return _task.Current;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Dispose()
                {
                    _task.Dispose();
                    _decrementConcurrentOperationsCounter();
                }

                //required to avoid boxing when the profiler queries the task name (GENERATE_NAME):
                //without the override, the virtual call on the struct boxes it and
                //ValueType.ToString allocates the type name on every new task instance
                public override string ToString() => TypeCache<TTask>.name;

                TTask          _task;
                readonly Action _decrementConcurrentOperationsCounter;
            }
        }
        
        public class MultiThreadedParallelTaskCollection<TTask> : BaseMultiThreadedParallelTaskCollection<TTask>, IEnumerator, IDisposable
                where TTask : struct, IParallelTask
        {
            public object Current
            {
                get => throw new NotImplementedException();
            }

            public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
                    : base(name, numberOfThreads, tightTasks) { }

            public MultiThreadedParallelTaskCollection(string name, bool tightTasks): this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2),
                tightTasks) { }
        }
    }
}