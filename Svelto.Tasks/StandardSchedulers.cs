namespace Svelto.Tasks
{
    public static class StandardSchedulers
    {
        public static IRunner multiThreadScheduler { get; private set; }

#if UNITY_5 || UNITY_5_3_OR_NEWER
        public static IRunner coroutineScheduler { get; private set; }
        public static IRunner physicScheduler { get; private set; }
        public static IRunner lateScheduler { get; private set; }
        public static IRunner updateScheduler { get; private set; }
#endif

        //physicScheduler -> updateScheduler -> coroutineScheduler -> lateScheduler

        static StandardSchedulers()
        {
            multiThreadScheduler = new MultiThreadRunner("MultiThreadRunner", true);
#if UNITY_5 || UNITY_5_3_OR_NEWER
            coroutineScheduler = new CoroutineMonoRunner("StandardCoroutineRunner");
            physicScheduler = new PhysicMonoRunner("StandardPhysicRunner");
            lateScheduler = new LateMonoRunner("StandardLateRunner");
            updateScheduler = new UpdateMonoRunner("StandardMonoRunner");
#endif
        }

        public static void StopSchedulers()
        {
            multiThreadScheduler.StopAllCoroutines();
#if UNITY_5 || UNITY_5_3_OR_NEWER
            coroutineScheduler.StopAllCoroutines();           
            physicScheduler.StopAllCoroutines();
            lateScheduler.StopAllCoroutines();
            updateScheduler.StopAllCoroutines();
#endif
        }
    }
}
