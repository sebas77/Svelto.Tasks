using System.Collections;

namespace Svelto.Tasks.Chain
{
    public interface ITaskChain<Token> : IEnumerator
    {
        Token token { set; }
    }
}
