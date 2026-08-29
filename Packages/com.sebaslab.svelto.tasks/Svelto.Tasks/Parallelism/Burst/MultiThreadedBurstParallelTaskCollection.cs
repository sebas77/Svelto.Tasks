
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Common;
using Svelto.Tasks;
using Svelto.Tasks.Parallelism;

/// <summary>
/// Splits concrete Burst range tasks into fixed-size, atomically claimed segments.
/// One reusable dispatcher is scheduled per MultiThreadRunner; each dispatcher
/// executes at most one segment per MoveNext so control always returns to the
/// runner between Burst calls. This preserves cooperative stop/yield behavior
/// without queueing and wrapping every segment as an independent Svelto task.
/// </summary>
public sealed class MultiThreadedBurstParallelTaskCollection<TTask> : IEnumerator, IDisposable
    where TTask : unmanaged, IBurstParallelTask
{
    public MultiThreadedBurstParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
    {
        DBC.Tasks.Check.Require(numberOfThreads > 0, "a parallel collection must run on at least one thread");

        _name = name;
        _workerState = new WorkerState();
        _workers = new Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WorkerDispatcher>(
            name.FastConcat(".Dispatchers"), numberOfThreads, tightTasks);
        _runEnumerator = new RunEnumerator(this);

        for (uint i = 0; i < numberOfThreads; i++)
            _workers.Add(new WorkerDispatcher(_workerState));
    }

    public event Action onComplete;

    public bool isRunning { private set; get; }

    public void Add(in TTask prototype, int iterations, int elementsPerTask)
    {
        DBC.Tasks.Check.Require(isRunning == false,
            "can't add tasks on a started MultiThreadedBurstParallelTaskCollection");
        DBC.Tasks.Check.Require(elementsPerTask > 0, "elementsPerTask must be greater than zero");
        if (iterations <= 0)
            return;

        //Store one immutable work definition, not one TTask per segment. Dispatchers
        //copy this prototype and set the claimed range immediately before execution.
        _workerState.Add(prototype, iterations, elementsPerTask);
    }

    public bool MoveNext()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return false;

        if (isRunning == false)
        {
            //Every wave reuses the same dispatcher tasks. Only the shared atomic cursor
            //and cancellation state need resetting before their runners start stepping.
            _workerState.BeginRun();
            isRunning = true;
        }

        if (_workers.MoveNext())
            return true;

        isRunning = false;
        onComplete?.Invoke();
        return false;
    }

    public IEnumerator<TaskContract> Run()
    {
        return _runEnumerator;
    }

    public void Stop()
    {
        Stop(0);
    }

    public void Stop(int msTimeout)
    {
        if (isRunning == false)
            return;

        _workerState.Cancel();
        _workers.Stop(msTimeout);
        isRunning = false;
    }

    public void Reset()
    {
        Stop();
        _workerState.Clear();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        //a constructor that threw (only reachable through the finalizer) leaves any of
        //the disposable fields unassigned: check each one independently
        if (_workerState != null)
            _workerState.Cancel();

        if (_workers != null)
            _workers.Dispose();

        if (_workerState != null)
            _workerState.DisposeDefinitions();

        onComplete = null;
        isRunning = false;
        GC.SuppressFinalize(this);
    }

    public object Current => null;

    public override string ToString()
    {
        return _name;
    }

    ~MultiThreadedBurstParallelTaskCollection()
    {
        Svelto.Console.LogWarning(
            $"MultiThreadedBurstParallelTaskCollection {_name} wasn't disposed of correctly. You forgot to call Dispose()");
        Dispose();
    }

    sealed class WorkerState
    {
        internal void Add(in TTask prototype, int iterations, int elementsPerTask)
        {
            int chunkCount = iterations / elementsPerTask;
            if (iterations % elementsPerTask != 0)
                chunkCount++;

            _definitions.Add(new RangeDefinition(
                prototype, iterations, elementsPerTask, _totalChunks, chunkCount));
            _totalChunks += chunkCount;
        }

        internal void BeginRun()
        {
            Volatile.Write(ref _cancelled, 0);
            Volatile.Write(ref _nextChunk, 0);
        }

        internal bool TryClaim(out TTask task, out int startIndex, out int count)
        {
            task = default;
            startIndex = 0;
            count = 0;

            if (Volatile.Read(ref _cancelled) != 0)
                return false;

            //All dispatchers race on one cursor. The atomic increment gives each caller a
            //unique segment; a runner that finishes early naturally claims more segments.
            int chunkIndex = Interlocked.Increment(ref _nextChunk) - 1;
            if (chunkIndex >= _totalChunks)
                return false;

            for (int i = 0; i < _definitions.Count; i++)
            {
                RangeDefinition definition = _definitions[i];
                if (chunkIndex >= definition.firstChunk + definition.chunkCount)
                    continue;

                int localChunk = chunkIndex - definition.firstChunk;
                startIndex = localChunk * definition.elementsPerTask;
                count = Math.Min(definition.elementsPerTask, definition.iterations - startIndex);
                task = definition.prototype;
                return true;
            }

            return false;
        }

        internal void Cancel()
        {
            //Dispatchers observe cancellation before their next claim. A Burst call already
            //in progress remains non-preemptive, bounding stop latency to one segment.
            Volatile.Write(ref _cancelled, 1);
        }

        internal void Clear()
        {
            _definitions.Clear();
            _totalChunks = 0;
            Volatile.Write(ref _nextChunk, 0);
        }

        internal void DisposeDefinitions()
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                TTask prototype = _definitions[i].prototype;
                prototype.Dispose();
            }

            Clear();
        }

        readonly List<RangeDefinition> _definitions = new List<RangeDefinition>();
        int _totalChunks;
        int _nextChunk;
        int _cancelled;
    }

    readonly struct RangeDefinition
    {
        internal RangeDefinition(in TTask prototype, int iterations, int elementsPerTask,
                                 int firstChunk, int chunkCount)
        {
            this.prototype = prototype;
            this.iterations = iterations;
            this.elementsPerTask = elementsPerTask;
            this.firstChunk = firstChunk;
            this.chunkCount = chunkCount;
        }

        internal readonly TTask prototype;
        internal readonly int iterations;
        internal readonly int elementsPerTask;
        internal readonly int firstChunk;
        internal readonly int chunkCount;
    }

    struct WorkerDispatcher : IParallelTask
    {
        internal WorkerDispatcher(WorkerState state)
        {
            _state = state;
            _currentTask = default;
            _hasCurrentTask = false;
        }

        public bool MoveNext()
        {
            //A multi-step IBurstParallelTask keeps its current range across runner steps.
            //The normal range-task contract returns false after one Burst direct call.
            if (_hasCurrentTask)
            {
                if (_currentTask.MoveNext())
                    return true;

                _currentTask.Dispose();
                _hasCurrentTask = false;
                return true;
            }

            if (_state.TryClaim(out _currentTask, out int startIndex, out int count) == false)
                return false;

            _currentTask.SetRange(startIndex, count);
            _hasCurrentTask = true;

            //Invoke at most one range-task step here. Even when the range completes,
            //return true below so MultiThreadRunner regains control before another claim.
            if (_currentTask.MoveNext())
                return true;

            _currentTask.Dispose();
            _hasCurrentTask = false;

            //The wrapper around this dispatcher stays alive and is stepped again; no new
            //range wrapper is admitted to the runner and no shared work queue is involved.
            return true;
        }

        public void Dispose()
        {
            if (_hasCurrentTask)
            {
                _currentTask.Dispose();
                _hasCurrentTask = false;
            }
        }

        public void Reset()
        {
        }

        public object Current => null;

        public override string ToString()
        {
            return TypeCache<TTask>.name;
        }

        readonly WorkerState _state;
        TTask _currentTask;
        bool _hasCurrentTask;
    }

    sealed class RunEnumerator : IEnumerator<TaskContract>
    {
        internal RunEnumerator(MultiThreadedBurstParallelTaskCollection<TTask> owner)
        {
            _owner = owner;
        }

        public bool MoveNext()
        {
            return _owner.MoveNext();
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }

        public TaskContract Current => TaskContract.Continue.It;
        object IEnumerator.Current => Current;

        readonly MultiThreadedBurstParallelTaskCollection<TTask> _owner;
    }

    readonly string _name;
    readonly WorkerState _workerState;
    readonly Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<WorkerDispatcher> _workers;
    readonly RunEnumerator _runEnumerator;
    int _isDisposed;
}

