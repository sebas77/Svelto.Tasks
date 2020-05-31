using Svelto.Common;

namespace Svelto.Tasks.Internal
{
    public interface IProcessSveltoTasks
    {
        bool MoveNext<PlatformProfiler>(in PlatformProfiler platformProfiler)
            where PlatformProfiler : IPlatformProfiler;
    }
}