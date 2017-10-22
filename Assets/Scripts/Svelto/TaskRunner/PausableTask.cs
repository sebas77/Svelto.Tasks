///
/// Unit tests to write:
/// Restart a task with compiled generated IEnumerator
/// Restart a task with IEnumerator class
/// Restart a task after SetEnumerator has been called (this must be still coded, as it must reset some values)
/// Restart a task just restarted (pendingRestart == true)
/// Start a taskroutine twice with different compiler generated enumerators and variants
/// 
/// 

using System;
using System.Collections;
using System.Runtime.CompilerServices;
#if NETFX_CORE
using System.Reflection;
#endif

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
    class PausableTask : ITaskRoutine, IPausableTask
    {
        public object Current
        {
            get
            {
                if (_coroutine != null)
                    return _coroutine.Current;

                return null;
            }
        }

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
            _compilerGenerated = IsCompilerGenerated(taskEnumerator.GetType());
#else
            _compilerGenerated = false;
#endif

            return this;
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
#if NETFX_CORE
                    _name = _taskGenerator.GetMethodInfo().DeclaringType.ToString().FastConcat(".", _taskGenerator.GetMethodInfo().Name);
#else
                    _name = _taskGenerator.Method.ReflectedType.ToString().FastConcat(".",  _taskGenerator.Method.Name);
#endif
            }

            return _name;
        }

        public bool MoveNext()
        {
            if (_stopped == true || _runner.isStopping == true)
            {
                _completed = true;

                if (_onStop != null)
                    _onStop();
            }
            else    
            if (_runner.paused == false && _paused == false)
            {
                try
                {
#if DEBUG
                    if (_started == false)
                        throw new Exception("Enumerating PausableTask without starting it, please call Start() first");
#endif
                    _completed = !_coroutine.MoveNext();

                    var current = _coroutine.Current;
                    if (current == Break.It ||
                        current == Break.AndStop)
                    {
                        Stop();

                        _completed = true;

                        if (_onStop != null)
                            _onStop();
                    }
                }
                catch (Exception e)
                {
                    _completed = true;

                    FinalizeIt();

                    if (_onFail != null && (e is TaskYieldsIEnumerableException) == false)
                        _onFail(new PausableTaskException(e));
                    else
                        throw new PausableTaskException(e);
                }
            }

            if (_completed == true)
            {
                FinalizeIt();

                if (_pendingRestart == true)
                    //start new coroutine using this task
                    Restart(_pendingEnumerator);

                return false;
            }

            return true;
        }

        private void FinalizeIt()
        {
            _coroutineWrapper.FastClear();
            Stop();

            if (_pool != null)
                _pool.PushTaskBack(this);

            _enumeratorWrap.Completed();
        }

        //Reset task on reuse, when fetched from the Pool
        public void Reset()
        {
            _pendingEnumerator = null;
            _taskGenerator     = null;
            _taskEnumerator    = null;

            _runner            = null;
            _onFail            = null;
            _onStop            = null;

            _stopped           = false;
            _paused            = false;
            _threadSafe        = false;
            _compilerGenerated = false;
            _completed         = false;
            _started           = false;
            _pendingRestart    = false;
            _name              = string.Empty;
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
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

        public void Stop()
        {
            //pay attention, completed cannot be put to true here, because it the task restarts
            //it must ends naturally through the fact that _stopped is true
            _stopped = true;
            _started = false;
        }

        internal PausableTask(IPausableTaskPool pool) : this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            _enumeratorWrap = new EnumeratorWrapper();
            _coroutineWrapper = new SerialTaskCollection(1);

            Reset();
        }

        bool IsCompilerGenerated(Type t)
        {
#if NETFX_CORE
            var attr = t.GetTypeInfo().GetCustomAttribute(typeof(CompilerGeneratedAttribute));
#else
            var attr = Attribute.GetCustomAttribute(t, typeof(CompilerGeneratedAttribute));
#endif

            return attr != null;
        }

        /// <summary>
        /// A Pausable Task cannot be recycled from the pool if hasn't been
        /// previously completed. The Pending logic is valid for normal
        /// tasks that are held and reused by other classes.
        /// </summary>
        /// <param name="task"></param>
        void InternalStart()
        {
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

            _taskEnumeratorJustSet = false;
            _stopped               = false;
            _completed             = false;
            _started               = true;
            _pendingEnumerator     = null;
            _pendingRestart        = false;
            _enumeratorWrap.Reset();

            SetTask(task);

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
                _coroutineWrapper.Add(task);
                _coroutine = _coroutineWrapper;
            }
            else
                _coroutine = taskc;
        }

        IRunner                       _runner;
        IEnumerator                   _coroutine;
        SerialTaskCollection          _coroutineWrapper;

        bool                          _stopped;
        bool                          _paused;
        bool                          _threadSafe;
        bool                          _compilerGenerated;
        bool                          _pendingRestart;
        bool                          _taskEnumeratorJustSet;

        IEnumerator                   _pendingEnumerator;
        IEnumerator                   _taskEnumerator;
        EnumeratorWrapper             _enumeratorWrap;

        readonly IPausableTaskPool    _pool;
        Func<IEnumerator>             _taskGenerator;

        Action<PausableTaskException> _onFail;
        Action                        _onStop;
        string                        _name = String.Empty;

        volatile bool _completed = false;
        volatile bool _started = false;       

        class EnumeratorWrapper : IEnumerator
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

            public object Current { get; private set; }

            volatile bool _completed;
        }
    }
}
