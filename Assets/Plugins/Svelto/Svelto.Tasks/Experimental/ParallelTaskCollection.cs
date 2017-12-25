using System;

namespace Svelto.Tasks.Experimental
{
    public class ParallelTaskCollection<Token> : ParallelTaskCollection
    {
        public TaskCollection Add(ITaskChain<Token> task)
        {
            if (task == null)
                throw new ArgumentNullException();

            Add(new TaskWrapper<Token>(task) { token = _token });

            return this;
        }

        public ParallelTaskCollection(Token token) { _token = token; }

        protected override void CheckForToken(object current)
        {
            var task = current as IChainLink<Token>;
            if (task != null)
                task.token = _token;
        }

        Token _token;
    }
}
