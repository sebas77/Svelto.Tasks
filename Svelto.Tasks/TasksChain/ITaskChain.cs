using System.Collections.Generic;
using Svelto.Tasks.Unity;

namespace Svelto.Tasks.Chain
{
    public interface ITaskChain<Token> : IEnumerator<TaskContract?>
    {
        Token token { set; }
    }
}
