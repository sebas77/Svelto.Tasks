using System.Collections;

namespace Svelto.Tasks.ExtraLean
{
    public interface IExtraLeanRunner<T>: IRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
    {
    }
    
    public interface IGenericExtraLeanRunner: IRunner<ExtraLeanSveltoTask<IEnumerator>>
    {
    }
}