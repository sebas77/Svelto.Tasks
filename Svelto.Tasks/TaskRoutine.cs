#if ENABLE_PLATFORM_PROFILER || TASKS_PROFILER_ENABLED || (DEBUG && !PROFILER)
#define GENERATE_NAME
#endif

using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Internal
{
    public sealed class TaskRoutine<TTask>: ISveltoTask, ITaskRoutine<TTask> where TTask: IEnumerator<TaskContract>
    {
        const string CALL_START_FIRST_ERROR = "Enumerating PausableTask without starting it, please call Start() first";
        
        private TaskRoutine()
        {}
        
        public TaskRoutine(IInternalRunner<TaskRoutine<TTask>> runner)
        {
            _sveltoTask = new SveltoTask();
            _runner     = runner;
            _continuationEnumerator = ContinuationWrapperPool.RetrieveFromPool();
        }

        public bool MoveNext()
        {
            if (_sveltoTask.MoveNext(_onFail, _onStop) == false)
            {
                if (_sveltoTask._threadSafeSveltoTaskStates.pendingTask == true)
                {
                    _previousContinuationEnumerator.Completed();

                    //start new coroutine using this task this will put _started to true (it was already though)
                    //it uses the current runner to start the pending task
                    DBC.Tasks.Check.Require(_runner != null, "SetScheduler function has never been called");

                    _sveltoTask._stackingTask = new SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>>(ref _pendingTask);
                    _sveltoTask._threadSafeSveltoTaskStates = new SveltoTaskState();
                    _continuationEnumerator.Reset();
                }
                else
                    _continuationEnumerator.Completed();
                
                _pendingTask                 = default(TTask);
                _previousContinuationEnumerator = null;

                return false;
            }

            return true;
        }

        public TaskContract Current
        {
            get { return _sveltoTask.Current; }
        }
        
        /// <summary>
        /// Calling SetEnumeratorProvider, SetEnumerator
        /// on a running task won't stop the task until either 
        /// Stop() or Start() is called.
        /// </summary>
        /// <param name="runner"></param>
        /// <returns></returns>
        public void SetEnumeratorProvider(Func<TTask> taskGenerator)
        {
            _taskGenerator = taskGenerator;
        }

        public void SetEnumerator(TTask taskEnumerator)
        {
            _taskGenerator = null;

            if (IS_TASK_STRUCT == true || (IEnumerator)_taskEnumerator != (IEnumerator) taskEnumerator)
                _sveltoTask._threadSafeSveltoTaskStates.taskEnumeratorJustSet = true;

            _taskEnumerator = taskEnumerator;
        }
        
        public ContinuationEnumerator Start(Action<SveltoTaskException> onFail = null, Action onStop = null)
        {
            DBC.Tasks.Check.Require(_taskGenerator != null || _taskEnumerator != null,
                                    "An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");
            
            _onStop = onStop;
            _onFail = onFail;

            return Start(false);
        }
        
        public ContinuationEnumerator StartImmediate()
        {
            return Start(true);
        }

        ContinuationEnumerator Start(bool immediate)
        {
            Pause();

            var continuationWrapper = _continuationEnumerator; 

            var newTask = _taskGenerator != null ? _taskGenerator() : _taskEnumerator;

            if (_sveltoTask._threadSafeSveltoTaskStates.isRunning == true
             && _sveltoTask._threadSafeSveltoTaskStates.explicitlyStopped == true)
            {
                //Stop() Start() causes this (previous continuation wrapper will stop before to start the new one)
                //Start() Start() is perceived as a continuation of the previous task therefore it won't
                //cause the continuation wrapper to stop
                _pendingTask                 = newTask;
                _previousContinuationEnumerator = continuationWrapper;

                continuationWrapper = _continuationEnumerator = new ContinuationEnumerator();
            }
            else
            {
                DBC.Tasks.Check.Require(_runner != null, "SetScheduler function has never been called");

                if (_taskGenerator == null && _sveltoTask._threadSafeSveltoTaskStates.taskEnumeratorJustSet == false)
                {
#if GENERATE_NAME
                    DBC.Tasks.Check.Assert(newTask.GetType().IsCompilerGenerated() == false, "Cannot restart a compiler generated iterator block, use SetEnumeratorProvider instead ".FastConcat(this.ToString()));
#endif
                    newTask.Reset();
                }

                _sveltoTask._stackingTask = new SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>>(ref newTask);

                if (_sveltoTask._threadSafeSveltoTaskStates.isRunning == false)
                {
                    _sveltoTask._threadSafeSveltoTaskStates         = new SveltoTaskState();
                    _sveltoTask._threadSafeSveltoTaskStates.started = true;
                    var taskRoutine = this;
                    _runner.StartCoroutine(ref taskRoutine, immediate);
                }
            }
            
            Resume();

            return continuationWrapper;
        }

        public void Pause()
        {
            _sveltoTask._threadSafeSveltoTaskStates.paused = true;
        }

        public void Resume()
        {
            _sveltoTask._threadSafeSveltoTaskStates.paused = false;
        }

        public void Stop()
        {
            _sveltoTask.Stop();
        }

        public bool isRunning
        {
            get { return _sveltoTask._threadSafeSveltoTaskStates.isRunning; }
        }

        public bool isDone
        {
            get { return _sveltoTask._threadSafeSveltoTaskStates.isDone; }
        }

        public override string ToString()
        {
#if GENERATE_NAME
            if (_sveltoTask._name == null)
            {
                if (_taskGenerator == null && _taskEnumerator == null)
                    _sveltoTask._name = base.ToString();
                else
                if (_taskEnumerator != null)
                    _sveltoTask._name = _taskEnumerator.ToString();
                else
                {
                    var methodInfo = _taskGenerator.GetMethodInfoEx();
                    
                    _sveltoTask._name = methodInfo.GetDeclaringType().ToString().FastConcat(".", methodInfo.Name);
                }
            }
    
            return _sveltoTask._name;
#else
            return "TaskRoutine";
#endif            
        }


        SveltoTask              _sveltoTask;
        readonly IInternalRunner<TaskRoutine<TTask>> _runner;
        
        Action<SveltoTaskException> _onFail;
        Action                      _onStop;
        ContinuationEnumerator         _previousContinuationEnumerator;
        ContinuationEnumerator         _continuationEnumerator;
        TTask                       _pendingTask;
        Func<TTask>                 _taskGenerator;
        TTask                       _taskEnumerator;
        
        static readonly bool IS_TASK_STRUCT = typeof(TTask).IsClass == false && typeof(TTask).IsInterface == false;

        public struct SveltoTask
        {
            internal void Stop()
            {
                _threadSafeSveltoTaskStates.explicitlyStopped = true;
            }

            internal TaskContract Current
            {
                get
                {
                      return _stackingTask.Current;
                }
            }

            /// <summary>
            /// Move Next is called by the current runner, which could be on another thread! that means that the
            /// --->class states used in this function must be thread safe<-----
            /// </summary>
            /// <returns></returns>
            internal bool MoveNext(Action<SveltoTaskException> onFail = null, Action onStop = null)
            {
                /// Stop() can be called from whatever thread, but the runner won't know about it until the next MoveNext()
                /// is called. It's VERY important that a task is not reused until naturally stopped through this mechanism,
                /// otherwise there is the risk to add the same task twice in the runner queue. The new task must be added
                /// in the queue through the pending enumerator functionality
                try
                {
                    if (_threadSafeSveltoTaskStates.isNotCompletedAndNotPaused)
                    {
                        bool completed;
                        if (_threadSafeSveltoTaskStates.explicitlyStopped == true)
                        {
                            completed = true;

                            if (onStop != null)
                            {
                                try
                                {
                                    onStop();
                                }
                                catch (Exception onStopException)
                                {
                                    Console.LogException("Svelto.Tasks task OnStop callback threw an exception: "
                                                                      .FastConcat(base.ToString()), onStopException);
                                }
                            }
                        }
                        else
                        {
                            try
                            {
#if DEBUG && !PROFILER
                                DBC.Tasks.Check.Assert(_threadSafeSveltoTaskStates.started == true, _callStartFirstError);
#endif
                                completed = true;
                                        completed = !_stackingTask.MoveNext();

                                       if (_stackingTask.Current.breakit == Break.AndStop && onStop != null)
                                {
                                    try
                                    {
                                        completed = true;

                                        onStop();
                                    }
                                    catch (Exception onStopException)
                                    {
                                        Console
                                           .LogException("Svelto.Tasks task OnStop callback threw an exception: "
                                                            .FastConcat(base.ToString()), onStopException);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                completed = true;

                                if (onFail != null && (e is TaskYieldsIEnumerableException) == false)
                                {
                                    try
                                    {
                                        onFail(new SveltoTaskException(e));
                                    }
                                    catch (Exception onFailException)
                                    {
                                        Console
                                           .LogException("Svelto.Tasks task OnFail callback threw an exception: "
                                                            .FastConcat(base.ToString()), onFailException);
                                    }
                                }
                                else
                                {
                                    Console.LogException("a Svelto.Tasks task threw an exception:  "
                                                                .FastConcat(base.ToString()), e);
                                }
                            }
                        }

                        if (completed == true)
                            _threadSafeSveltoTaskStates.completed = true;
                    }

                    if (_threadSafeSveltoTaskStates.isCompletedAndNotPaused == true)
                        return false;

                    return true;
                }
                catch (Exception e)
                {
                    Console.LogException(
                            new SveltoTaskException("Something went drastically wrong inside a PausableTask", e));

                    throw;
                }
            }

            internal SveltoTaskState _threadSafeSveltoTaskStates;
            internal SveltoTaskWrapper<TTask, IInternalRunner<LeanSveltoTask<TTask>>> _stackingTask;

#if GENERATE_NAME
        internal string                _name;
#endif
#if DEBUG && !PROFILER
#pragma warning disable 649
            string _callStartFirstError;
#pragma warning restore 649
#endif
        }
    }
}