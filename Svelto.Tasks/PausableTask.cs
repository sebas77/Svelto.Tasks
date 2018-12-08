///
/// Unit tests to write:
/// Restart a task with compiled generated IEnumerator
/// Restart a task with IEnumerator class
/// Restart a task after SetEnumerator has been called (this must be still coded, as it must reset some values)
/// Restart a task just restarted (pendingRestart == true)
/// Start a taskroutine twice with different compiler generated enumerators and variants
/// 
///
///

#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using Svelto.Utilities;
using System;
using System.Collections;

namespace Svelto.Tasks
{
    public class PausableTaskException : Exception
    {
        public PausableTaskException(Exception e)
            : base(e.ToString(), e)
        {
        }

        public PausableTaskException(string message, Exception e)
            : base(message.FastConcat(" -", e.ToString()), e)
        {
        }
    }

    public interface IPausableTask : IEnumerator
    {
        TaskCollection<IEnumerator>.CollectionTask Current { get; }
    }

    //The Continuation Wrapper contains a valid
    //value until the task is not stopped.
    //After that it should be released.
    public class ContinuationWrapper : IEnumerator
    {
        public bool MoveNext()
        {
            ThreadUtility.MemoryBarrier();
            var result = _completed;
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

        public object Current
        {
            get { return null; }
        }

        volatile bool _completed;
        Func<bool>    _condition;
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
            _taskGenerator  = taskGenerator;

            return this;
        }

        public ITaskRoutine SetEnumerator(IEnumerator taskEnumerator)
        {
            _taskGenerator = null;
            if (_taskEnumerator != taskEnumerator)
                _threadSafeStates.taskEnumeratorJustSet = true;
            _taskEnumerator = taskEnumerator;
            return this;
        }

        public void Pause()
        {
            _threadSafeStates.paused = true;
        }

        public void Resume()
        {
            _threadSafeStates.paused = false;
        }

        public void Stop()
        {
            _threadSafeStates.explicitlyStopped = true;

            OnTaskInterrupted();
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
            get { return _threadSafeStates.isRunning; }
        }

        public bool isDone
        {
            get { return _threadSafeStates.isDone; }
        }

        public ContinuationWrapper Start(Action<PausableTaskException> onFail = null, Action onStop = null)
        {
            Pause();
            
            _onStop = onStop;
            _onFail = onFail;

            OnTaskInterrupted();

            InternalStart();

            return _continuationWrapper;
        }

        object IEnumerator.Current
        {
            get
            {
                //this is currently the serial task
                if (_stackingTask != null)
                {
                    //this is the enumerator held by the serial task
                    return _stackingTask.Current;
                }

                return null;
            }
        }

        public TaskCollection<IEnumerator>.CollectionTask Current
        {
            get
            {
                //this is currently the serial task
                if (_stackingTask != null)
                {
                    //this is the enumerator held by the serial task
                    return _stackingTask.Current;
                }

                return new TaskCollection<IEnumerator>.CollectionTask(null);
            }
        }

        public override string ToString()
        {
#if GENERATE_NAME
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
#endif
            return "PausableTask";
        }

        /// <summary>
        /// Move Next is called by the current runner, which could be on another thread!
        /// that means that the --->class states used in this function must be thread safe<-----
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            /// Stop() can be called from whatever thread, but the runner won't know about it until the next MoveNext()
            /// is called. It's VERY important that a task is not reused until naturally stopped through this mechanism,
            /// otherwise there is the risk to add the same task twice in the runner queue. The new task must be added
            /// in the queue through the pending enumerator functionality
            try
            {
                if (_runner.paused == true) return true;

                if (_threadSafeStates.isNotCompletedAndNotPaused)
                {
                    bool completed;
                    if (_threadSafeStates.explicitlyStopped == true || _runner.isStopping == true)
                    {
                        completed = true;

                        if (_onStop != null)
                        {
                            try
                            {
                                _onStop();
                            }
                            catch (Exception onStopException)
                            {
                                Utilities.Console.LogException("Svelto.Tasks task OnStop callback threw an exception: "
                                                                  .FastConcat(base.ToString()), onStopException);
                            }
                        }
                    }
                    else
                    {
#if DEBUG && !PROFILER
                        DBC.Tasks.Check.Assert(_threadSafeStates.started == true, _callStartFirstError);
#endif
                        try
                        {
                            completed = !_stackingTask.MoveNext();

                            var current = _stackingTask.Current;
                            if (current.breakIt == Break.AndStop && _onStop != null)
                            {
                                try
                                {
                                    completed = true;

                                    _onStop();
                                }
                                catch (Exception onStopException)
                                {
                                    Utilities.Console
                                       .LogException("Svelto.Tasks task OnStop callback threw an exception: "
                                                        .FastConcat(base.ToString()), onStopException);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            completed = true;

                            if (_onFail != null && (e is TaskYieldsIEnumerableException) == false)
                            {
                                try
                                {
                                    _onFail(new PausableTaskException(e));
                                }
                                catch (Exception onFailException)
                                {
                                    Utilities.Console
                                       .LogException("Svelto.Tasks task OnFail callback threw an exception: "
                                                        .FastConcat(base.ToString()), onFailException);
                                }
                            }
                            else
                            {
                                Utilities.Console.LogException("a Svelto.Tasks task threw an exception:  "
                                                            .FastConcat(base.ToString()), e);
                            }
                        }
                    }
                    
                    if (completed == true)
                        _threadSafeStates.completed = true;
                }

                if (_threadSafeStates.isCompletedAndNotPaused == true)
                {
                    if (_pool != null)
                    {
                        DBC.Tasks.Check.Assert(_threadSafeStates.pendingRestart == false,
                                               "a pooled task cannot have a pending restart!");

                        CleanUpOnRecycle();

                        _continuationWrapper.Completed();
                        _pool.PushTaskBack(this);
                    }
                    else
                    {
                        //TaskRoutine case only!! This is the most risky part of this code when the code enters here, another
                        //thread could be about to set a pending restart!
                        
                        if (_threadSafeStates.pendingRestart == true)
                        {
                            _pendingContinuationWrapper.Completed();

                            //start new coroutine using this task this will put _started to true (it was already though)
                            //it uses the current runner to start the pending task
                            Restart(_pendingTask, false);
                        }
                        else
                            _continuationWrapper.Completed();
                    }

                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Utilities.Console.LogException(
                        new PausableTaskException("Something went drastically wrong inside a PausableTask", e));

                throw;
            }
        }

        /// <summary>
        /// Reset task on reuse, when fetched from the Pool
        /// </summary>
        public void Reset()
        {
            CleanUpOnRecycle();

            //todo: avoid to allocate here, otherwise defeats the purposes of the pooling
            _continuationWrapper = new ContinuationWrapper();

#if GENERATE_NAME
            _name = string.Empty;
#endif
            _threadSafeStates = new State();
        }

        /// <summary>
        /// Clean up task on complete. This function doesn't need to reset any state, is only to release resources
        /// before it goes back to the pool!!!
        /// It's called only for the pooled tasks and will be called again on reset
        /// </summary>
        internal void CleanUpOnRecycle()
        {
            _pendingTask          = null;
            _pendingContinuationWrapper = null;

            _taskGenerator  = null;
            _taskEnumerator = null;
            _runner         = null;
            _onFail         = null;
            _onStop         = null;

            _coroutineWrapper.Clear();

            ClearInvokes();
        }

        /// <summary>
        /// Clean up task on Restart can happen only through ITaskRoutine when they restart
        /// </summary>
        void CleanUpOnRestart()
        {
            _pendingTask          = null;
            _pendingContinuationWrapper = null;

            _threadSafeStates = new State();
            _threadSafeStates.started = true;

            _continuationWrapper.Reset();

            ClearInvokes();
        }

        internal PausableTask(PausableTaskPool pool) : this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            _coroutineWrapper    = new SerialTaskCollection(1);
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
            DBC.Tasks.Check.Require(_threadSafeStates.pendingRestart == false,
                "a task has been reused while is pending to start");
            DBC.Tasks.Check.Require(_taskGenerator != null || _taskEnumerator != null,
                "An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");

            var newTask = _taskEnumerator ?? _taskGenerator();

            //TaskRoutine case only!!
            bool isTaskRoutineIsAlreadyIn = _pool                       == null
                                         && _threadSafeStates.isRunning == true;

            if (isTaskRoutineIsAlreadyIn            == true
             && _threadSafeStates.explicitlyStopped == true)
            {
                //Stop() Start() causes this (previous continuation wrapper will stop before to start the new one)
                //Start() Start() is perceived as a continuation of the previous task therefore it won't
                //cause the continuation wrapper to stop
                _pendingTask                     = newTask;
                _pendingContinuationWrapper      = _continuationWrapper;
                _threadSafeStates.pendingRestart = true;

                _continuationWrapper = new ContinuationWrapper();
                
                Resume(); //if it's paused, must resume

                return;
            }

            Restart(newTask, isTaskRoutineIsAlreadyIn);
        }

        void Restart(IEnumerator task, bool isTaskRoutineIsAlreadyIn)
        {
            DBC.Tasks.Check.Require(_runner != null, "SetScheduler function has never been called");

            if (_taskEnumerator != null && _threadSafeStates.taskEnumeratorJustSet == false)
            {
#if DEBUG && !PROFILER
                DBC.Tasks.Check.Assert(_taskEnumerator.GetType().IsCompilerGenerated() == false, "Cannot restart an IEnumerator without a valid Reset function, use SetEnumeratorProvider instead ".FastConcat(_name));
#endif
                task.Reset();
            }

            SetTask(task);
            CleanUpOnRestart();

            if (isTaskRoutineIsAlreadyIn == false)
            {
                _runner.StartCoroutine(this);
            }
        }

        void SetTask(IEnumerator task)
        {
            if (task is TaskCollection<IEnumerator>)
            {
                _stackingTask = (TaskCollection<IEnumerator>) task;
            }
            else
            {
                _coroutineWrapper.Clear();
                _coroutineWrapper.Add(task);
                
                _stackingTask = _coroutineWrapper;
            }
                
#if DEBUG && !PROFILER
            _callStartFirstError = CALL_START_FIRST_ERROR.FastConcat(" task: ", ToString());
#endif
        }

        IRunner                     _runner;
        TaskCollection<IEnumerator> _stackingTask;

        ContinuationWrapper _continuationWrapper;
        ContinuationWrapper _pendingContinuationWrapper;

        IEnumerator _pendingTask;
        IEnumerator _taskEnumerator;

        Func<IEnumerator> _taskGenerator;

        Action<PausableTaskException> _onFail;
        Action                        _onStop;

        State _threadSafeStates;

        readonly PausableTaskPool                  _pool;
        readonly SerialTaskCollection<IEnumerator> _coroutineWrapper;

#if GENERATE_NAME
        string                        _name = String.Empty;
#endif
#if DEBUG && !PROFILER
        string                        _callStartFirstError;
#endif

        struct State
        {
            volatile byte _value;

            const byte COMPLETED_BIT            = 0x1;
            const byte STARTED_BIT              = 0x2;
            const byte EXPLICITLY_STOPPED       = 0x4;
            const byte TASK_ENUMERATOR_JUST_SET = 0x8;
            const byte PAUSED_BIT               = 0x10;
            const byte PENDING_BIT              = 0x20;

            public bool completed
            {
                get { return BIT(COMPLETED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(COMPLETED_BIT);
                    else 
                        UNSETBIT(COMPLETED_BIT);
                }
            }

            public bool explicitlyStopped
            {
                get { return BIT(EXPLICITLY_STOPPED); }
                set
                {
                    if (value) 
                        SETBIT(EXPLICITLY_STOPPED);
                    else 
                        UNSETBIT(EXPLICITLY_STOPPED);
                }
            }

            public bool paused
            {
                get { return BIT(PAUSED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(PAUSED_BIT);
                    else 
                        UNSETBIT(PAUSED_BIT);
                }
            }

            public bool started
            {
                get { return BIT(STARTED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(STARTED_BIT);
                    else 
                        UNSETBIT(STARTED_BIT);
                }
            }

            public bool taskEnumeratorJustSet
            {
                get { return BIT(TASK_ENUMERATOR_JUST_SET); }
                set
                {
                    if (value) 
                        SETBIT(TASK_ENUMERATOR_JUST_SET);
                    else 
                        UNSETBIT(TASK_ENUMERATOR_JUST_SET);
                }
            }

            public bool pendingRestart
            {
                get { return BIT(PENDING_BIT); }
                set
                {
                    if (value) 
                        SETBIT(PENDING_BIT);
                    else 
                        UNSETBIT(PENDING_BIT);
                }
            }

            void SETBIT(byte bitmask)
            {
                ThreadUtility.VolatileWrite(ref _value, (byte) (_value | bitmask));
            }

            void UNSETBIT(int bitmask)
            {
                ThreadUtility.VolatileWrite(ref _value, (byte) (_value & ~bitmask));
            }

            bool BIT(byte bitmask)
            {
                return (ThreadUtility.VolatileRead(ref _value) & bitmask) == bitmask;
            }

            public bool isRunning
            {
                get
                {
                    byte completedAndStarted = STARTED_BIT | COMPLETED_BIT;

                    //started but not completed
                    return (ThreadUtility.VolatileRead(ref _value) & completedAndStarted) == STARTED_BIT;
                }
            }

            public bool isDone
            {
                get
                {
                    byte completedAndStarted = COMPLETED_BIT | STARTED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndStarted) == COMPLETED_BIT;
                }
            }

            public bool isNotCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndPaused) == 0x0;
                }
            }
            
            public bool isCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndPaused) == COMPLETED_BIT;
                }
            }


            public bool isRunningAnOldTask
            {
                get
                {
                    byte startedAndMustRestart             = STARTED_BIT | EXPLICITLY_STOPPED | TASK_ENUMERATOR_JUST_SET;
                    byte completedAndStartedAndMustRestart = (byte) (startedAndMustRestart | COMPLETED_BIT);

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndStartedAndMustRestart) ==
                           startedAndMustRestart; //it's set to restart, but not completed
                }
            }
        }
    }
}