
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public static class StandardSchedulers
    {
        public static IRunner multiThreadScheduler { get; private set; }
        public static IRunner mainThreadScheduler { get; private set; }
        public static IRunner physicScheduler { get; private set; }
        public static IRunner syncScheduler { get; private set; }

        static StandardSchedulers()
        {
            mainThreadScheduler = new MonoRunner();
            syncScheduler = new SyncRunner();
            multiThreadScheduler = new MultiThreadRunner();
            physicScheduler = new PhysicMonoRunner();
        }

        public static void StopSchedulers()
        {
            mainThreadScheduler.StopAllCoroutines();
            multiThreadScheduler.StopAllCoroutines();
            physicScheduler.StopAllCoroutines();
        }
    }
}
