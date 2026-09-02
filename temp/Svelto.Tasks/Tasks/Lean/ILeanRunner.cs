using System.Collections.Generic;

namespace Svelto.Tasks.Lean
{
    public interface IGenericLeanRunner: IRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
        bool isValid { get; }
    }
    
    public interface IGenericLeanRunner<T>: IRunner<LeanSveltoTask<T>> where T : IEnumerator<TaskContract>
    {
    }
}