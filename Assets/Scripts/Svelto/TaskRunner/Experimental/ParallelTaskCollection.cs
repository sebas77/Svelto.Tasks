namespace Svelto.Tasks.Experimental
{
    public class ParallelTaskCollection<Token> : ParallelTaskCollection
    {
        override protected TaskWrapper CreateTaskWrapper(IAbstractTask task)
        {
            return new TaskWrapper<Token>(task) { token = _token };
        }

        public ParallelTaskCollection(Token token) { _token = token; }

        override protected void CheckForToken(object current)
        {
            var task = current as IChainLink<Token>;
            if (task != null)
                task.token = _token;
        }

        Token _token;
    }
}
