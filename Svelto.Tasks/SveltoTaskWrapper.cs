using System.Collections.Generic;

namespace Svelto.Tasks
{
    struct SveltoTaskWrapper<TTask, TRunner> where TTask : IEnumerator<TaskContract>
                                             where TRunner:class, IRunner<Lean.SveltoTask<TTask>>
    {
        public SveltoTaskWrapper(ref TTask task, TRunner runner):this()
        {
            _taskContinuation._runner = runner;
            _task   = task;
        }

        public bool MoveNext()
        {
            var continuationWrapper = _current.Continuation;
            if (continuationWrapper != null)
            {
                //a task is waiting to be completed, spin this one
                if (continuationWrapper.Value.isRunning == true) 
                    return true;

                //this is a continued task
                if (_taskContinuation._continuingTask != null)
                {
                    //the child task is telling to interrupt everything!
                    var currentBreakit = _taskContinuation._continuingTask.Current.breakit;
                    _taskContinuation._continuingTask = null;
                    
                    if (currentBreakit == Break.AndStop)
                        return false;
                }
            }
            
            //continue the normal execution of this task
            if (_task.MoveNext() == false) 
                return false;
    
            _current = _task.Current;
                 
            if (_current.yieldIt == true) 
                return true;
    
            if (_current.breakit == Break.It || _current.breakit == Break.AndStop || _current.hasValue == true)
                return false;

            if (_current.enumerator != null)
            {
                //Current.enumerator is a "continued" enumerator and can be only a class at the moment
                _taskContinuation._continuingTask = _current.enumerator;

                //a new TaskContract is created, holding the continuationEnumerator of the new task
                var continuation = ((TTask) _taskContinuation._continuingTask).RunImmediate(_taskContinuation._runner);

                _current = new TaskContract(continuation);
            }

            return true;
        }

        TTask        _task;
        ContinueTask _taskContinuation;
        TaskContract _current;

        struct ContinueTask
        {
            internal TRunner _runner;
            internal IEnumerator<TaskContract>   _continuingTask;
        }

        internal TTask task => _task;
    }
}