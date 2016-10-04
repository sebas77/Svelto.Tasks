namespace Svelto.Tasks.Experimental
{
    public class SerialTaskCollection<Token> : SerialTaskCollection
    {
        public SerialTaskCollection(Token token) { _token = token; }

        protected override TaskWrapper CreateTaskWrapper(IAbstractTask task)
        {
            return new TaskWrapper<Token>(task) { token = _token };
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
