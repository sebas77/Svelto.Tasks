using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Tasks.ExtraLean;

namespace Svelto.Tasks.Parallelism
{
    public class MultiThreadedParallelTaskCollectionException : Exception
    {
        public MultiThreadedParallelTaskCollectionException(
            string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection) : base(
            canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection)
        {
        }
    }
    
    public interface IParallelTask: IEnumerator, IDisposable
    {
    }

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
            DBC.Tasks.Check.Require(numberOfThreads > 0, "doesn't make much sense to use this with 0 threads");

            _name = name;

            InitializeThreadsAndData(numberOfThreads, tightTasks);
        }
        
        public BaseMultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }

        /// <summary>
        /// Add can be called by another thread, so if the collection is already running
        /// I can't allow adding more tasks.
        /// </summary>
        /// <param name="enumerator"></param>
        /// <exception cref="MultiThreadedParallelTaskCollectionException"></exception>
        public void Add(in TTask enumerator)
        {
            if (isRunning == true)
                throw new MultiThreadedParallelTaskCollectionException(
                    "can't add tasks on a started MultiThreadedParallelTaskCollection");

            _parallelTasks.Add(enumerator);
        }

        public bool MoveNext()
        {
            //isDisposed can be set by the GC finalizer thread
            if (Volatile.Read(ref _isDisposed)) return false;

            if (RunMultiThreadParallelTasks()) return true;

            // finished naturally: runners can be reused, so don't dispose them here
            isRunning = false;

            if (onComplete != null)
                onComplete();

            return false;
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
            if (Volatile.Read(ref _isDisposed) == true) return;

            Volatile.Write(ref _isDisposed, true);

            // If tasks were never started, the only place they exist is _parallelTasks.
            // If tasks were started, their Dispose will be called by the runners.
            if (isRunning == false)
            {
                for (int i = 0; i < _parallelTasks.Count; i++)
                    _parallelTasks[i].Dispose();
            }

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
            Console.LogWarning(
                $"MultiThreadedParallelTaskCollection {_name} wasn't disposed of correctly. You forgot to call Dispose()");

            Dispose();
        }

        void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
        {
            _runners = new Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>[numberOfThreads];

            //prepare a single multithread runner for each group of fiber like task collections
            //number of threads can be less than the number of tasks to run
            for (int i = 0; i < numberOfThreads; i++)
                _runners[i] = new Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>(
                    "MultiThreadedParallelRunner ".FastConcat(_name, " #").FastConcat(i), false, tightTasks);
        }

        bool RunMultiThreadParallelTasks()
        {
            if (_isDisposed == true)
                return false;

            if (isRunning == false)
            {
                if (_parallelTasks.Count == 0) return false;

                isRunning = true;
                Volatile.Write(ref _counter, _parallelTasks.Count);

                //start them
                for (int i = 0; i < _parallelTasks.Count; i++)
                {
                    var runner = _runners[i % _runners.Length];
                    var wrapper = Wrap(_parallelTasks[i]);
                    wrapper.RunOn( runner);
                }
            }

            //wait for completion, I am not using signaling as this Collection could be yielded by a main thread runner
            return Volatile.Read(ref _counter) > 0;
        }

        WrapEnumerator Wrap(in TTask task)
        {
            return new WrapEnumerator(task, _decrementConcurrentOperationsCounterDelegate);
        }

        void DecrementConcurrentOperationsCounter()
        {
            Interlocked.Decrement(ref _counter);
        }

        protected Svelto.Tasks.ExtraLean.Struct.MultiThreadRunner<WrapEnumerator>[] _runners;
        readonly List<TTask>  _parallelTasks = new List<TTask>();

        int  _counter;
        bool _isDisposed;

        readonly string _name;
        readonly Action _decrementConcurrentOperationsCounterDelegate;
        
        protected struct WrapEnumerator : IEnumerator, IDisposable
        {
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

            TTask          _task;
            readonly Action _decrementConcurrentOperationsCounter;
        }
    }
}

namespace Svelto.Tasks.Parallelism.Lean
{
    public class MultiThreadedParallelTaskCollection<TTask> : BaseMultiThreadedParallelTaskCollection<TTask>, IEnumerator<TaskContract>
        where TTask : struct, IEnumerator<TaskContract>, IParallelTask
    {
        public TaskContract Current
        {
            get { return TaskContract.Yield.It; }
        }

        object IEnumerator.Current
        {
            get => throw new NotImplementedException();
        }

        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
            : base(name, numberOfThreads, tightTasks)
        {
        }

        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }
    }
    
    public class MultiThreadedParallelTaskCollection: BaseMultiThreadedParallelTaskCollection<IParallelTask>, IEnumerator<TaskContract>
    {
        public TaskContract Current
        {
            get { return TaskContract.Yield.It; }
        }

        object IEnumerator.Current
        {
            get => throw new NotImplementedException();
        }

        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
                : base(name, numberOfThreads, tightTasks)
        {
        }

        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }
    }
}

namespace Svelto.Tasks.Parallelism.ExtraLean
{
    public class MultiThreadedParallelTaskCollection<TTask> : BaseMultiThreadedParallelTaskCollection<TTask>, IEnumerator, IDisposable
        where TTask : struct, IParallelTask
    {
        public object Current
        {
            get => throw new NotImplementedException();
        }

        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
            : base(name, numberOfThreads, tightTasks)
        {
        }
        
        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }
    }
    
    public class MultiThreadedParallelTaskCollection : BaseMultiThreadedParallelTaskCollection<IParallelTask>, IEnumerator, IDisposable
    {
        public object Current
        {
            get => throw new NotImplementedException();
        }

        public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
                : base(name, numberOfThreads, tightTasks)
        {
        }
        
        public MultiThreadedParallelTaskCollection(string name, bool tightTasks):this(name, (uint)Math.Max(1, Environment.ProcessorCount - 2), tightTasks)
        { }
    }
}
