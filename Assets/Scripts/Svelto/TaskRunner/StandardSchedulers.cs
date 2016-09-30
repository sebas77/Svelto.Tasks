
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public static class StandardSchedulers
    {
        public static IRunner multiThreadScheduler { get; private set; }
        public static IRunner endOfFrameScheduler { get; private set; }
        public static IRunner mainThreadScheduler { get; private set; }
        public static IRunner syncScheduler { get; private set; }

        static StandardSchedulers()
        {
            mainThreadScheduler = new MonoRunner();
            syncScheduler = new SyncRunner();
            multiThreadScheduler = new MultiThreadRunner();
        }

        public static void StopSchedulers()
        {
            mainThreadScheduler.StopAllCoroutines();
            multiThreadScheduler.StopAllCoroutines();
        }
    }
}
