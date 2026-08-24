using System.Collections.Generic;

namespace Svelto.Tasks.Lean
{
    public interface ILeanRunner<T>: IRunner<LeanSveltoTask<T>> where T : IEnumerator<TaskContract>
    {
    }
    
    public interface IGenericLeanRunner: IRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
    {
    }
    
    public interface IGenericLeanRunner<T>: IRunner<LeanSveltoTask<T>> where T : IEnumerator<TaskContract>
    {
    }
}