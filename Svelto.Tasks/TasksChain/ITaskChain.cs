using System.Collections.Generic;

namespace Svelto.Tasks.Chain
{
    public interface ITaskChain<Token> : IEnumerator<TaskContract>
    {
        Token token { set; }
    }
}
