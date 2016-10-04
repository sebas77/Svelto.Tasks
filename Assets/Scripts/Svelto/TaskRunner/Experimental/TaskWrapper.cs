namespace Svelto.Tasks.Experimental
{
    public class TaskWrapper<Token> : TaskWrapper
    {
        internal Token token { set; private get; }

        public TaskWrapper(IAbstractTask task)
            : base(task)
        { }

        override protected void ExecuteTask()
        {
            var chain = task as ITaskChain<Token>;
            if (chain != null)
                chain.Execute(token);
            else
                base.ExecuteTask();
        }
    }
}
