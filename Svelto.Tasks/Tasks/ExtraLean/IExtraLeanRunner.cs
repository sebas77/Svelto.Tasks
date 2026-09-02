using System.Collections;

namespace Svelto.Tasks.ExtraLean
{
    namespace Struct
    {
        public interface IExtraLeanRunner<T>: IRunner<ExtraLeanSveltoTask<T>> where T : struct, IEnumerator { }
    }

    public interface IExtraLeanRunner<T>: IRunner<ExtraLeanSveltoTask<T>> where T : class, IEnumerator
    {
    }
    
    public interface IGenericExtraLeanRunner: IRunner<ExtraLeanSveltoTask<IEnumerator>>
    {
    }
}