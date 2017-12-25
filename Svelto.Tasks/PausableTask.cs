///
/// Unit tests to write:
/// Restart a task with compiled generated IEnumerator
/// Restart a task with IEnumerator class
/// Restart a task after SetEnumerator has been called (this must be still coded, as it must reset some values)
/// Restart a task just restarted (pendingRestart == true)
/// Start a taskroutine twice with different compiler generated enumerators and variants
/// 
/// 

using Svelto.Utilities;
using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class PausableTaskException : Exception
    {
        public PausableTaskException(Exception e)
            : base(e.ToString(), e)
        { }
    }

    public interface IPausableTask:IEnumerator
    {}
}

namespace Svelto.Tasks.Internal
{
    sealed class PausableTask : IPausableTask, ITaskRoutine
    {
        const string CALL_START_FIRST_ERROR = "Enumerating PausableTask without starting it, please call Start() first";
        /// <summary>
        /// Calling SetScheduler, SetEnumeratorProvider, SetEnumerator
        /// on a running task won't stop the task until either 
        /// Stop() or Start() is called.
        /// </summary>
        /// <param name="runner"></param>
        /// <returns></returns>
        public ITaskRoutine SetScheduler(IRunner runner)
        {
            _runner = runner;

            return this;
        }

        public ITaskRoutine SetEnumeratorProvider(Func<IEnumerator> taskGenerator)
        {
            _taskEnumerator = null;
            _taskGenerator = taskGenerator;

            return this;
        }

        public ITaskRoutine SetEnumerator(IEnumerator taskEnumerator)
        {
            _taskGenerator = null;
            if (_taskEnumerator != taskEnumerator)
                _taskEnumeratorJustSet = true;
            _taskEnumerator = taskEnumerator;
#if DEBUG && !PROFILER
            _compilerGenerated = taskEnumerator.GetType().IsCompilerGenerated();
#else
            _compilerGenerated = false;
#endif
            return this;
        }

        public void Pause()
        {
            _paused = true;
            ThreadUtility.MemoryBarrier();
        }

        public void Resume()
        {
            _paused = false;
            ThreadUtility.MemoryBarrier();
        }

        public void Stop()
        {
            _explicitlyStopped = true;

            ThreadUtility.MemoryBarrier();
        }

        public IEnumerator Start(Action<PausableTaskException> onFail = null, Action onStop = null)
        {
            _threadSafe = false;

            _onStop = onStop;
            _onFail = onFail;

            InternalStart();

            return _enumeratorWrap;
        }

        public IEnumerator ThreadSafeStart(Action<PausableTaskException> onFail = null, Action onStop = null)
        {
            _threadSafe = true;

            _onStop = onStop;
            _onFail = onFail;
            
            InternalStart();

            return _enumeratorWrap;
        }

        public object Current
        {
            get
            {
                if (_coroutine != null)
                    return _coroutine.Current;

                return null;
            }
        }

        public override string ToString()
        {
            if (_name == string.Empty)
            {
                if (_taskGenerator == null && _taskEnumerator == null)
                    _name = base.ToString();
                else
                if (_taskEnumerator != null)
                    _name = _taskEnumerator.ToString();
                else
                {
                    System.Reflection.MethodInfo methodInfo = _taskGenerator.GetMethodInfoEx();
                    _name = methodInfo.GetDeclaringType().ToString().FastConcat(".", methodInfo.Name);
                }
            }

            return _name;
        }

        /// <summary>
        /// Move Next is called by the current runner, which could be on another thread!
        /// that means that the class states must be thread safe.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            ///
            /// Stop() can be called from whatever thread, but the 
            /// runner won't know about it until the next MoveNext()
            /// is called. It's VERY important that a task is not reused
            /// until naturally stopped through this mechanism, otherwise
            /// there is the risk to add the same task twice in the 
            /// runner queue. The new task must be added in the queue
            /// through the pending enumerator functionality
            /// 
            if (_explicitlyStopped == true || _runner.isStopping == true)
                TaskIsDone(_explicitlyStopped);
            else    
            if (_runner.paused == false && _paused == false)
            {
                try
                {
#if DEBUG
                    if (_started == false)
                        throw new 
                            Exception(CALL_START_FIRST_ERROR.FastConcat(" task: ", ToString()));
#endif
                    _completed = !_coroutine.MoveNext();

                    var current = _coroutine.Current;
                    if (current == Break.It ||
                        current == Break.AndStop)
                    {
                        TaskIsDone(true);
                    }
                }
                catch (Exception e)
                {
                    TaskIsDone(false);

                    if (_onFail != null && (e is TaskYieldsIEnumerableException) == false)
                        _onFail(new PausableTaskException(e));
                    else
                    {
                       Utility.Console.LogError(e.ToString());
                    }
                }
            }

            if (_completed == true)
            {
                if (_pendingRestart == true)
                    //start new coroutine using this task
                    Restart(_pendingEnumerator);
                else
                {
                    if (_pool != null)
                        _pool.PushTaskBack(this);

                    FinalizeIt();
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Reset task on reuse, when fetched from the Pool
        /// </summary>
        public void Reset()
        {
            CleanUp();

            //_enumeratorWrap.Reset cannot be inside 
            //CleanUp because it could be iterated way
            //after the task is completed.
            _enumeratorWrap.Reset();

            _paused = false;
            _taskEnumeratorJustSet = false;
            _completed = false;
            _started = false;
            _explicitlyStopped = false;
            _threadSafe = false;
            _compilerGenerated = false;
            _pendingRestart = false;
            _name = string.Empty;
        }

        /// <summary>
        /// Clean up task on complete
        /// </summary>
        internal void CleanUp()
        {
            _pendingEnumerator = null;
            _taskGenerator = null;
            _taskEnumerator = null;
            _runner = null;
            _onFail = null;
            _onStop = null;

            _coroutineWrapper.FastClear();
        }

        /// <summary>
        /// Clean up task on Restart 
        /// can happen only through ITaskRoutine
        /// </summary>
        void CleanUpOnRestart()
        {
            _paused = false;
            _taskEnumeratorJustSet = false;
            _completed = false;
            _explicitlyStopped = false;
            _pendingEnumerator = null;
            _pendingRestart = false;
            _name = string.Empty;
        }

        void TaskIsDone(bool stoppedExplicitly)
        {
            _completed = true;

            if (stoppedExplicitly == true && _onStop != null)
                _onStop();
        }

        void FinalizeIt()
        {
            _started = false;
            
            _enumeratorWrap.Completed();

            ThreadUtility.MemoryBarrier();
        }

        internal PausableTask(PausableTaskPool pool) : this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            _enumeratorWrap = new EnumeratorWrapper();
            _coroutineWrapper = new SerialTaskCollection(1);

            Reset();
        }

        /// <summary>
        /// A Pausable Task cannot be recycled from the pool if hasn't been
        /// previously completed. The Pending logic is valid for normal
        /// tasks that are held and reused by other classes.
        /// </summary>
        /// <param name="task"></param>
        void InternalStart()
        {
            _enumeratorWrap.Reset();

            Resume(); //if it's paused, must resume

            if (_pendingRestart == false) //ignore the restart otherwise
            {
                if (_taskGenerator == null && _taskEnumerator == null)
                    throw new Exception("An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");

                var originalEnumerator = _taskEnumerator ?? _taskGenerator();
                
                if (_started == true && _completed == false)
                {
                    Stop(); //if it's reused, must stop naturally

                    _pendingEnumerator = originalEnumerator;
                    _pendingRestart = true;
                }
                else
                    Restart(originalEnumerator);
            }
        }

        void Restart(IEnumerator task)
        {
            if (_taskEnumerator != null && _completed == true)
            {
                if (_taskEnumeratorJustSet == false)
                {
                    if (_compilerGenerated == false)
                        task.Reset();
                    else
                        throw new Exception(
                            "Cannot restart an IEnumerator without a valid Reset function, use SetEnumeratorProvider instead");
                }
            }

            if (_runner == null)
                throw new Exception("SetScheduler function has never been called");

            CleanUpOnRestart();
            SetTask(task);

            _started = true;

            if (_threadSafe == false)
                _runner.StartCoroutine(this);
            else
                _runner.StartCoroutineThreadSafe(this);
        }

        void SetTask(IEnumerator task)
        {
            var taskc = task as TaskCollection;

            if (taskc == null)
            {
                _coroutineWrapper.FastClear();
                _coroutineWrapper.Add(task);
                _coroutine = _coroutineWrapper;
            }
            else
                _coroutine = taskc;
        }

        IRunner                       _runner;
        IEnumerator                   _coroutine;

        readonly SerialTaskCollection _coroutineWrapper;
        readonly EnumeratorWrapper    _enumeratorWrap;

        bool                          _threadSafe;
        bool                          _compilerGenerated;
        bool                          _taskEnumeratorJustSet;

        IEnumerator                   _pendingEnumerator;
        IEnumerator                   _taskEnumerator;
      
        readonly PausableTaskPool     _pool;
        Func<IEnumerator>             _taskGenerator;

        Action<PausableTaskException> _onFail;
        Action                        _onStop;
        string                        _name = String.Empty;

        volatile bool                 _completed;
        volatile bool                 _started;
        volatile bool                 _explicitlyStopped;
        volatile bool                 _paused;
        volatile bool                 _pendingRestart;

        sealed class EnumeratorWrapper : IEnumerator
        {
            public bool MoveNext()
            {
                if (_completed == true)
                {
                    _completed = false;
                    return false;
                }
                return _completed == false;
            }

            public void Completed()
            {
                _completed = true;
            }

            public void Reset()
            {
                _completed = false;
            }

            public object Current { get { return null; } }

            bool _completed;
        }
    }
}
