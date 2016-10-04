using System;
using System.Collections;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public class PausableTaskException : Exception
    {
        public PausableTaskException(ITaskRoutine task, Exception e)
            : base(e.Message, e)
        {
            _task = task;
        }

        public ITaskRoutine _task;
    }
}

namespace Svelto.Tasks
{
    public class PausableTask : ITaskRoutine, IEnumerator
    {
        internal PausableTask(IPausableTaskPool pool):this()
        {
            _pool = pool;
        }

        internal PausableTask()
        {
            _stopped = true;
            _enumeratorWrap = EnumeratorWrapper();
            _enumerator = new CoroutineEx();
        }

        public object Current
        {
            get
            {
                if (_enumerator != null)
                    return _enumerator.Current;

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
            _taskEnumerator = taskEnumerator;

            return this;
        }

        public bool MoveNext()
        {
            if (_stopped == true || _runner.stopped == true)
            {
                _completed = true;

                //this is needed to avoid to create multiple CoRoutine when
                //Stop and Start are called in the same frame
                if (_pendingRestart == true)
                {
                    _pendingRestart = false;
                    //start new coroutine using this task
                    Restart(_pendingEnumerator);

                    return false;
                }

                if (_onStop != null)
                    _onStop();
            }
            else    
            if (_runner.paused == false && _paused == false)
            {
                try
                {
                    _completed = !_enumerator.MoveNext();

                    if (_enumerator.Current == Break.It)
                    {
                        _completed = true;
                        if (_onStop != null)
                            _onStop();
                    }
                }
                catch (Exception e)
                {
                    if (_onFail != null)
                        _onFail(new PausableTaskException(this, e));
                    else
                        throw;

                    _completed = true;
                }   
            }

            if (_completed == true && _pool != null)
                _pool.PushTaskBack(this);

            return !_completed;
        }

        public void Reset()
        {
            _pendingEnumerator = null;
            _taskGenerator = null;
            _taskEnumerator = null;
            _runner = null;
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
        }

        /// <summary>
        /// A Pausable Task cannot be recycled from the pool if hasn't been
        /// previously completed. The Pending logic is valid for normal
        /// tasks that are held and reused by other classes.
        /// </summary>
        /// <param name="task"></param>
        void InternalStart()
        {
            if (_taskGenerator == null && _taskEnumerator == null)
                throw new Exception("An enumerator or enumerator provider is required to enable this function, please use SetEnumeratorProvider/SetEnumerator before to call start");

            Resume(); //if it's paused, must resume

            IEnumerator enumerator = _taskEnumerator ?? _taskGenerator();

            if (_completed == false)
            {
                _stopped = true; //if it's reused, must stop naturally
                _pendingEnumerator = enumerator;
                _pendingRestart = true;
            }
            else
                Restart(enumerator);
        }

        void Restart(IEnumerator task)
        {
            if (_runner == null)
                throw new Exception("SetScheduler function has never been called");

            _stopped = false;
            _completed = false;

            SetTask(task);

            _runner.StartCoroutine(this);
        }

        void SetTask(IEnumerator task)
        {
            if ((task is CoroutineEx) == false)
                _enumerator.Reuse(task);
            else
                _enumerator = task as CoroutineEx;
        }

        IEnumerator EnumeratorWrapper()
        {
            while (_completed == false)
                yield return null;

            yield return _enumerator;
        }

        IRunner                         _runner;
        CoroutineEx                     _enumerator;
        bool                            _stopped;
        bool                            _paused;
        volatile bool                   _completed = true;
        bool                            _pendingRestart;
        IEnumerator                     _pendingEnumerator;
        IPausableTaskPool               _pool;
        Func<IEnumerator>               _taskGenerator;
        IEnumerator                     _taskEnumerator;
        Action<PausableTaskException>   _onFail;
        Action                          _onStop;
        IEnumerator                     _enumeratorWrap;
    }
}

