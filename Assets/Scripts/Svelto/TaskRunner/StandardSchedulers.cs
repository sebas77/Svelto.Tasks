
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    public static class StandardSchedulers
    {
        public static IRunner multiThreadScheduler { get; private set; }
        public static IRunner coroutineScheduler { get; private set; }
        public static IRunner physicScheduler { get; private set; }
        public static IRunner syncScheduler { get; private set; }
        public static IRunner lateScheduler { get; private set; }
        public static IRunner updateScheduler { get; private set; }

        //physicScheduler -> updateScheduler -> coroutineScheduler -> lateScheduler

        static StandardSchedulers()
        {
            coroutineScheduler = new CoroutineMonoRunner("StandardCoroutineRunner");
            syncScheduler = new SyncRunner(false);
            multiThreadScheduler = new MultiThreadRunner(true);
            physicScheduler = new PhysicMonoRunner("StandardPhysicRunner");
            lateScheduler = new LateMonoRunner("StandardLateRunner");
            updateScheduler = new StandardMonoRunner("StandardMonoRunner");
        }

        public static void StopSchedulers()
        {
            coroutineScheduler.StopAllCoroutines();
            multiThreadScheduler.StopAllCoroutines();
            physicScheduler.StopAllCoroutines();
            lateScheduler.StopAllCoroutines();
            updateScheduler.StopAllCoroutines();
        }
    }
}
