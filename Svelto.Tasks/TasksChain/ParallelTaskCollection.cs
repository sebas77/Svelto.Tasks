using System;

namespace Svelto.Tasks.Chain
{
    public class ParallelTaskCollection<Token> : Svelto.Tasks.ParallelTaskCollection<ITaskChain<Token>>, ITaskChain<Token>
    {
        public ParallelTaskCollection(Token token):base()
        {
            this.token = token;
        }

        public ParallelTaskCollection<Token> Add(ITaskChain<Token> task)
        {
            if (task == null)
                throw new ArgumentNullException();
            
            base.Add(ref task);

            return this;
        }

        protected override void ProcessTask(ref ITaskChain<Token> current)
        {
            current.token = token;
        }

        public Token token { get; set; }
    }
}
