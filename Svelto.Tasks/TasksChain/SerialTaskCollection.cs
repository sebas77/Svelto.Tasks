using System;

namespace Svelto.Tasks.Chain
{
    public class SerialTaskCollection<Token> : Svelto.Tasks.SerialTaskCollection<ITaskChain<Token>>, ITaskChain<Token>
    {
        public SerialTaskCollection(Token token) { this.token = token; }
        public SerialTaskCollection() {}
        
        public SerialTaskCollection<Token> Add(ITaskChain<Token> task)
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
