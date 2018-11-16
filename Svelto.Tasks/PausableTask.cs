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
        
        public PausableTaskException(string message, Exception e)
            : base(message.FastConcat(" -", e.ToString()), e)
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

        internal event Action onTaskHasBeenInterrupted;
       
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
            _taskEnumeratorJustSet = true;
            _taskEnumerator = taskEnumerator;
#if DEBUG && !PROFILER
            _compilerGenerated = taskEnumerator.GetType().IsCompilerGenerated();
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

            OnTaskInterrupted();

            ThreadUtility.MemoryBarrier();
        }

        void OnTaskInterrupted()
        {
            if (onTaskHasBeenInterrupted != null)
            {
                onTaskHasBeenInterrupted.Invoke();
                ClearInvokes();
            }
        }

        void ClearInvokes()
        {
            if (onTaskHasBeenInterrupted == null) return;
            foreach (var d in onTaskHasBeenInterrupted.GetInvocationList())
            {
                onTaskHasBeenInterrupted -= (Action) d;
            }
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
            _syncPoint = true;
            ThreadUtility.MemoryBarrier();
            
            _onStop = onStop;
            _onFail = onFail;
            
            OnTaskInterrupted();
            
            InternalStart();
            
            _syncPoint = false;
            ThreadUtility.MemoryBarrier();
            
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
#if DEBUG && !PROFILER            
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
#endif
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
            /// 
            try
            {
                ThreadUtility.MemoryBarrier();

                if (_completed == false && _syncPoint == false)
                {
                    if (_explicitlyStopped == true || _runner.isStopping == true)
                    {
                        _completed = true;

                        if (_onStop != null)
                        {
                            try
                            {
                                _onStop();
                            }
                            catch (Exception onStopException)
                            {
                                Utilities.Console.LogError("Svelto.Tasks task OnStop callback threw an exception ", ToString());
                                Utilities.Console.LogException(onStopException);
                            }
                        }
                    }
                    else
                        if (_runner.paused == false && _paused == false)
                        {
#if DEBUG && !PROFILER
                            DBC.Tasks.Check.Assert(_started == true, _callStartFirstError);
#endif
                            try
                            {
                                _completed = !_coroutine.MoveNext();
                                
                                ThreadUtility.MemoryBarrier();
                                
                                var current = _coroutine.Current;
                                if ((current == Break.It ||
                                     current == Break.AndStop) && _onStop != null)
                                {
                                    try
                                    {
                                        _onStop();
                                    }
                                    catch (Exception onStopException)
                                    {
                                        Utilities.Console.LogError("Svelto.Tasks task OnStop callback threw an exception ", ToString());
                                        Utilities.Console.LogException(onStopException);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                _completed = true;

                                if (_onFail != null && (e is TaskYieldsIEnumerableException) == false)
                                {
                                    try
                                    {
                                        _onFail(new PausableTaskException(e));
                                    }
                                    catch (Exception onFailException)
                                    {
                                        Utilities.Console.LogError("Svelto.Tasks task OnFail callback threw an exception ", ToString());
                                        Utilities.Console.LogException(onFailException);
                                    }
                                }
                                else
                                {
                                    Utilities.Console.LogError("a Svelto.Tasks task threw an exception: ", ToString());
                                    Utilities.Console.LogException(e);
                                }
                            }
                        }
                }

                if (_completed == true && ThreadUtility.VolatileRead(ref _syncPoint) == false)
                {
                    if (_pool != null)
                    {
                        DBC.Tasks.Check.Assert(_pendingRestart == false,
                                               "a pooled task cannot have a pending restart!");

                        CleanUpOnRecycle();
                        _continuationWrapper.Completed();
                        _pool.PushTaskBack(this);
                    }
                    else
                        //TaskRoutine case only!! This is the most risky part of this code when the code enters here, another
                        //thread could be about to set a pending restart!
                    {
                        ThreadUtility.MemoryBarrier();
                        if (_pendingRestart == true)
                        {
                            _pendingContinuationWrapper.Completed();

                            //start new coroutine using this task this will put _started to true (it was already though)
                            //it uses the current runner to start the pending task
                            Restart(_pendingEnumerator, false);
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
            catch (Exception e)
            {
                Utilities.Console.LogException(new PausableTaskException("Something went drastically wrong inside a PausableTask", e));

                throw;
            }
        }

        /// <summary>
        /// Reset task on reuse, when fetched from the Pool
        /// </summary>
        public void Reset()
        {
            CleanUpOnRecycle();

            _continuationWrapper =  new ContinuationWrapper();
    
            _paused = false;
            _taskEnumeratorJustSet = false;
            _completed = false;
            _started = false;
            _explicitlyStopped = false;
#if DEBUG && !PROFILER            
            _compilerGenerated = false;
#endif    
            _pendingRestart = false;
            _name = string.Empty;
        }

        /// <summary>
        /// Clean up task on complete. This function doesn't need to reset any state, is only to release resources before it goes back to the pool!!!
        /// It's called only for the pooled tasks and will be called again on reset
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

            ClearInvokes();
            _coroutineWrapper.FastClear();
        }

        /// <summary>
        /// Clean up task on Restart can happen only through ITaskRoutine when they restart
        /// </summary>
        void CleanUpOnRestart()
        {
            _pendingEnumerator          = null;
            _pendingContinuationWrapper = null;
            
            _paused = false;
            _taskEnumeratorJustSet = false;
            _completed = false;
            _explicitlyStopped = false;
            _pendingRestart = false;
            
            _coroutineWrapper.Clear();
            _continuationWrapper.Reset();
            ClearInvokes();
        }
        
        internal PausableTask(PausableTaskPool pool) : this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            TASKS_CREATED++;
#if !DEBUG || PROFILER             
            _name = TASKS_CREATED.ToString();
#endif            
            _coroutineWrapper = new SerialTaskCollection(1);
            _continuationWrapper = new ContinuationWrapper();

            Reset();
        }

        /// <summary>
        /// A PausableTask cannot be recycled from the pool if hasn't been previously completed.
        /// A task can actually be restarted, but this will stop the previous enumeration, even if the enumerator didn't
        /// change.
        /// However since an enumerator can be enumerated on another runner a task cannot set as completed immediately,
        /// but it must wait for the next MoveNext. This is what the Pending logic is about.
        /// </summary>
        /// <param name="task"></param>
        void InternalStart()
        {
            DBC.Tasks.Check.Require(_pendingRestart == false, "a task has been reused while is pending to start");
            DBC.Tasks.Check.Require(_taskGenerator != null || _taskEnumerator != null,
                                    "An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");

            Resume(); //if it's paused, must resume

            var originalEnumerator = _taskEnumerator ?? _taskGenerator();

            //TaskRoutine case only!!
            bool isTaskRoutineIsAlreadyIn = _pool      == null
                                         && _completed == false
                                         && _started   == true;

            if (isTaskRoutineIsAlreadyIn == true
             && _explicitlyStopped       == true)
            {
                //Stop() Start() cauess this (previous continuation wrapper will stop before to start the new one)
                //Start() Start() will not make the _continuationWrapper stop until the task is really completed
                _pendingEnumerator          = originalEnumerator;
                _pendingContinuationWrapper = _continuationWrapper;
                _pendingRestart             = true;

                _continuationWrapper = new ContinuationWrapper();

                return;
            }

            Restart(originalEnumerator, isTaskRoutineIsAlreadyIn);
        }

        void Restart(IEnumerator task, bool isTaskRoutineIsAlreadyIn)
        {
            DBC.Tasks.Check.Require(_runner != null, "SetScheduler function has never been called");
            
            if (_taskEnumerator != null && _taskEnumeratorJustSet == false)
            {
#if DEBUG && !PROFILER                
                DBC.Tasks.Check.Assert(_compilerGenerated == false, "Cannot restart an IEnumerator without a valid Reset function, use SetEnumeratorProvider instead ".FastConcat(_name));
#endif    
                task.Reset();
            }
            
            CleanUpOnRestart();
            SetTask(task);

            _started = true;
            
            if (isTaskRoutineIsAlreadyIn == false)
            {
                _syncPoint = false; //very important this to happen now
                ThreadUtility.MemoryBarrier();
                _runner.StartCoroutine(this);
            }
            else
            {
                ThreadUtility.MemoryBarrier();
            }
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
#if DEBUG && !PROFILER            
            _callStartFirstError = CALL_START_FIRST_ERROR.FastConcat(" task: ", ToString());
#endif    
        }

        IRunner                       _runner;
        IEnumerator                   _coroutine;

        internal readonly SerialTaskCollection _coroutineWrapper;
        
        ContinuationWrapper           _continuationWrapper;
        ContinuationWrapper           _pendingContinuationWrapper;

#if DEBUG && !PROFILER        
        bool                          _compilerGenerated;
#endif    
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
        volatile bool                 _syncPoint;
#if DEBUG && !PROFILER        
        string                        _callStartFirstError;
#endif
        static int TASKS_CREATED;
        
    }
}
