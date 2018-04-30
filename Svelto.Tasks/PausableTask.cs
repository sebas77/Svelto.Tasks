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
    
    //The Continuation Wrapper contains a valid
    //value until the task is not stopped.
    //After that it should be released.
    public class ContinuationWrapper : IEnumerator
    {
        public bool MoveNext()
        {
            ThreadUtility.MemoryBarrier();
            var result = completed == true;
            if (_condition != null)
                result |= _condition();
            
            if (result == true)
            {
                _condition = null;
                _completed = false;
                ThreadUtility.MemoryBarrier();
                return false;
            }
            
            return true;
        }
        
        public void BreakOnCondition(Func<bool> func)
        {
            _condition = func;
        }

        internal void Completed()
        {
            _completed = true;
            ThreadUtility.MemoryBarrier();
        }
        
        public bool completed
        {
            get { return _completed; }
        }        

        public void Reset()
        {
            _completed = false;
            ThreadUtility.MemoryBarrier();
        }

        public object Current { get { return null; } }

        volatile bool _completed;
        Func<bool> _condition;
    }

}

namespace Svelto.Tasks.Internal
{
    sealed class PausableTask : IPausableTask, ITaskRoutine
    {
        const string CALL_START_FIRST_ERROR = "Enumerating PausableTask without starting it, please call Start() first";
        
        internal Action onExplicitlyStopped { private get; set; }
        
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

            if (onExplicitlyStopped != null)
            {
                onExplicitlyStopped();
                onExplicitlyStopped = null;
            }

            ThreadUtility.MemoryBarrier();
        }

        public bool isRunning
        {
            get
            {
                return _started == true && _completed == false;
            }
        }

        public ContinuationWrapper Start(Action<PausableTaskException> onFail = null, Action onStop = null)
        {
            _threadSafe = false;

            _onStop = onStop;
            _onFail = onFail;
            
            InternalStart();

            return _continuationWrapper;
        }

        public ContinuationWrapper ThreadSafeStart(Action<PausableTaskException> onFail = null, Action onStop = null)
        {
            _threadSafe = true;

            _onStop = onStop;
            _onFail = onFail;
            
            InternalStart();

            return _continuationWrapper;
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
                    var methodInfo = _taskGenerator.GetMethodInfoEx();
                    
                    _name = methodInfo.GetDeclaringType().ToString().FastConcat(".", methodInfo.Name);
                }
            }

            return _name;
        }

        /// <summary>
        /// Move Next is called by the current runner, which could be on another thread!
        /// that means that the --->class states used in this function must be thread safe<-----
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
            /// DO NOT USE FUNCTIONS AS IT MUST BE CLEAR WHICH STATES ARE USED
            /// 
            /// threadsafe states:
            /// - _explicitlyStopped
            /// - _completed
            /// - _paused
            /// - _runner
            /// - _pool
            /// - _pendingRestart
            /// - _started
            /// 
            ThreadUtility.MemoryBarrier();
            if (_explicitlyStopped == true || _runner.isStopping == true)
            {
                _completed = true;
                
                ThreadUtility.MemoryBarrier();

                if (_onStop != null)
                    _onStop();
            }
            else    
            if (_runner.paused == false && _paused == false)
            {
                try
                {
                    DBC.Check.Assert(_started == true, _callStartFirstError);
                    
                    _completed = !_coroutine.MoveNext();
                    ThreadUtility.MemoryBarrier();
                    
                    var current = _coroutine.Current;
                    if (current == Break.It ||
                        current == Break.AndStop)
                    {
                        if (_onStop != null)
                            _onStop();
                    }
                }
                catch (Exception e)
                {
                    _completed = true;
                    ThreadUtility.MemoryBarrier();
                    
                    if (_onFail != null && (e is TaskYieldsIEnumerableException) == false)
                        _onFail(new PausableTaskException(e));
                    else
                    {
                       Utility.Console.LogException(e);
                    }
#if DEBUG
                    throw;
#endif
                }
            }

            if (_completed == true)
            {
                if (_pool != null)
                {
                    DBC.Check.Assert(_pendingRestart == false, "a pooled task cannot have a pending restart!");
                    
                    _continuationWrapper.Completed();
                    _pool.PushTaskBack(this);
                }
                else
                //TaskRoutine case only!! This is the most risky part of this code
                //when the code enters here, another thread could be about to
                //set a pending restart!
                {
                    ThreadUtility.MemoryBarrier();
                    if (_pendingRestart == true)
                    {
                        _pendingContinuationWrapper.Completed();
                        
                        //start new coroutine using this task
                        //this will put _started to true (it was already though)
                        //it uses the current runner to start the pending task
                        Restart(_pendingEnumerator);
                    }
                    else
                    {
                        _continuationWrapper.Completed();
                    }
                }

                ThreadUtility.MemoryBarrier();

                return false;
            }

            return true;
        }

        /// <summary>
        /// Reset task on reuse, when fetched from the Pool
        /// </summary>
        public void Reset()
        {
            CleanUpOnRecycle();

            //_enumeratorWrap.Reset cannot be inside 
            //CleanUp because it could be iterated way
            //after the task is completed.
            _continuationWrapper =  new ContinuationWrapper();
    
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
        /// This function doesn't need to
        /// reset any state, is only to
        /// release resources!!!
        /// </summary>
        internal void CleanUpOnRecycle()
        {
            _pendingEnumerator = null;
            _pendingContinuationWrapper = null;
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
            _pendingRestart = false;
            _name = string.Empty;
            
            _pendingEnumerator = null;
            _pendingContinuationWrapper = null;
            
            _coroutineWrapper.Clear();
            _continuationWrapper.Reset();
        }

        internal PausableTask(PausableTaskPool pool) : this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            _coroutineWrapper = new SerialTaskCollection(1);
            _continuationWrapper = new ContinuationWrapper();

            Reset();
        }

        /// <summary>
        /// A Pausable Task cannot be recycled from the pool if hasn't been
        /// previously completed.
        /// A task can actually be restarted, but this will stop the previous
        /// enumeration, even if the enumerator didn't change.
        /// However since an enumerator can be enumerated on another runner
        /// a task cannot set as completed immediatly, but it must wait for
        /// the next MoveNext. This is what the Pending logic is about.
        /// </summary>
        /// <param name="task"></param>
        void InternalStart()
        {
            DBC.Check.Require(_pendingRestart == false, "a task has been reused while is pending to start");
            DBC.Check.Require(_taskGenerator != null || _taskEnumerator != null, "An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");
            
            Resume(); //if it's paused, must resume
            
            var originalEnumerator = _taskEnumerator ?? _taskGenerator();
            
            //TaskRoutine case only!!
            ThreadUtility.MemoryBarrier();
            if (_pool == null 
                && _completed == false 
                && _started == true
                && _explicitlyStopped == true)
            {
                _pendingEnumerator = originalEnumerator;
                _pendingContinuationWrapper = _continuationWrapper;
                _pendingRestart = true;
                
                 _continuationWrapper = new ContinuationWrapper();
                
                ThreadUtility.MemoryBarrier();

                 return;
            }
            
            Restart(originalEnumerator);
        }

        void Restart(IEnumerator task)
        {
            DBC.Check.Require(_runner != null, "SetScheduler function has never been called");
            
            if (_taskEnumerator != null && _taskEnumeratorJustSet == false)
            {
                DBC.Check.Assert(_compilerGenerated == false, "Cannot restart an IEnumerator without a valid Reset function, use SetEnumeratorProvider instead ".FastConcat(_name));
                
                task.Reset();
            }
            
            CleanUpOnRestart();
            SetTask(task);

            _started = true;
            ThreadUtility.MemoryBarrier();

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
            
            _callStartFirstError = CALL_START_FIRST_ERROR.FastConcat(" task: ", ToString());
        }

        IRunner                       _runner;
        IEnumerator                   _coroutine;

        readonly SerialTaskCollection _coroutineWrapper;
        
        ContinuationWrapper           _continuationWrapper;
        ContinuationWrapper           _pendingContinuationWrapper;
        
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
        
        bool                          _started;

        volatile bool                 _completed;
        volatile bool                 _explicitlyStopped;
        volatile bool                 _paused;
        volatile bool                 _pendingRestart;
        
        string                        _callStartFirstError;
    }
}
