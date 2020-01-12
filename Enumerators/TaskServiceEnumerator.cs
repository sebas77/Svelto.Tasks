using System;
using System.Collections;
using Svelto.Tasks.Chain;

namespace Svelto.Tasks.Enumerators
{
    /// <summary>
    /// Transform an ITask to IEnumerator to be usable with the TaskRunner
    /// The only reason why this class is public, instead of internal, is
    /// because in this way you could have the option to return a Task from
    /// an IEnumerator function which could have multiple return objects 
    /// (some ITask some not). Otherwise it should never be used
    /// explicitly 
    /// </summary>
    public class TaskServiceEnumerator: IEnumerator
    {
        public object Current { get { return null; } }

        public TaskServiceEnumerator(IServiceTask task)
        {
            DBC.Tasks.Check.Require((task is IEnumerable == false) && (task is IEnumerator == false), "Tasks and IEnumerators are mutually exclusive");

            this.task = task;
            
            DBC.Tasks.Check.Ensure(task != null, "a valid task must be assigned");
        }

        public bool MoveNext()
        {
            if (_started == false)
            {
                ExecuteTask();

                _started = true;
            }
            
            if (task.isDone == false)
            {
                var taskException = task as IServiceTaskExceptionHandler;

                if ((taskException != null) && (taskException.throwException != null))
                    throw taskException.throwException;

                return true;
            }

            _started = false;

            return false;
        }

        public void Reset()
        {
            throw new NotImplementedException("Async Tasks cannot be reset");
        }

        public override string ToString()
        {
            return task.ToString();
        }

        protected virtual void ExecuteTask()
        {
            var task1 = task as IServiceTask;
            if (task1 != null)
                task1.Execute();    
            else
                throw new Exception("not supported task " + task.GetType());
        }

        protected IServiceTask task { get; private set; }

        bool _started;
    }

    public class TaskServiceEnumerator<Token> : TaskServiceEnumerator, ITaskChain<Token>
    {
        public TaskServiceEnumerator(IServiceTask task) : base(task)
        {}

        public Token token { get; set; }
    }
}

