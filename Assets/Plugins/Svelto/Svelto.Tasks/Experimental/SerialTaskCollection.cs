using System;

namespace Svelto.Tasks.Experimental
{
    public class SerialTaskCollection<Token> : SerialTaskCollection
    {
        public SerialTaskCollection(Token token) { _token = token; }

        public TaskCollection Add(ITaskChain<Token> task)
        {
            if (task == null)
                throw new ArgumentNullException();

            Add(new TaskWrapper<Token>(task) { token = _token });

            return this;
        }

        protected override TaskWrapper CreateTaskWrapper(IAbstractTask task)
        {
            var taskI = task as ITaskChain<Token>;

            if (taskI == null)
                return base.CreateTaskWrapper(task);

            return new TaskWrapper<Token>(taskI);
        }

        protected override void CheckForToken(object current)
        {
            var task = current as IChainLink<Token>;
            if (task != null)
                task.token = _token;
        }

        Token _token;
    }
}
