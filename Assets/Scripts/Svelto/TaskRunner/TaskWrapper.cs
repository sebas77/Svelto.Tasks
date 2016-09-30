using System;
using System.Collections;

namespace Svelto.Tasks
{
    /// <summary>
    /// Transform an ITask to IEnumerator to be usable with the TaskRunner
    /// The only reason why this class is public, instead of internal, is
    /// because in this way you could have the option to return a Task from
    /// an IEnumerator function which could have multiple return objects 
    /// (some ITask some not). Otherwise it should never be used
    /// explicitly 
    /// </summary>
    public class TaskWrapper: IEnumerator
    {
        public object         Current { get { return this; } }

        internal IAbstractTask task { get; private set; }
        
        public TaskWrapper(IAbstractTask task)
        {
            DesignByContract.Check.Require(task is IEnumerable == false && task is IEnumerator == false, "Tasks and IEnumerators are mutually exclusive");
            
            this.task = task;
            _enumerator = Execute();

            DesignByContract.Check.Ensure(task != null, "a valid task must be assigned");
        }

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public void Reset()
        {
            throw new NotImplementedException("Async Tasks cannot be reset");
        }

        public override string ToString()
        {
            return task.ToString();
        }

        virtual protected void ExecuteTask()
        {
            if (task is ITask)
                ((ITask)task).Execute();    
            else
                throw new Exception("not supported task " + task.GetType());
        }

        IEnumerator Execute()
        {
            ExecuteTask();            
            
            ITaskExceptionHandler taskException = null;

            if (task is ITaskExceptionHandler)
                taskException = (task as ITaskExceptionHandler);

            while (task.isDone == false)
            {
                if (taskException != null && taskException.throwException != null)
                    throw taskException.throwException;

                yield return null;
            }
        }

        IEnumerator _enumerator;
    }

    public class TaskWrapper<Token>: TaskWrapper
    {
        internal Token token { set; private get; }

        public TaskWrapper(IAbstractTask task):base(task)
        {}

        override protected void ExecuteTask()
        {
            if (task is ITaskChain<Token>)
                ((ITaskChain<Token>)task).Execute(token);
            else
                base.ExecuteTask();
        }
    }
}

