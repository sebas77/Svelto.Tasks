namespace Svelto.Tasks
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner _multiThreadScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER
        static CoroutineMonoRunner _coroutineScheduler;
        static PhysicMonoRunner _physicScheduler;
        static LateMonoRunner _lateScheduler;
        static UpdateMonoRunner _updateScheduler;
#endif

        public static IRunner multiThreadScheduler { get { if (_multiThreadScheduler == null) _multiThreadScheduler = new MultiThreadRunner("MultiThreadRunner", true);
            return _multiThreadScheduler;
        } }

#if UNITY_5 || UNITY_5_3_OR_NEWER
        public static IRunner coroutineScheduler { get { if (_coroutineScheduler == null) _coroutineScheduler = new CoroutineMonoRunner("StandardCoroutineRunner");
            return _coroutineScheduler;
        } }
        public static IRunner physicScheduler { get { if (_physicScheduler == null) _physicScheduler = new PhysicMonoRunner("StandardPhysicRunner");
            return _physicScheduler;
        } }
        public static IRunner lateScheduler { get { if (_lateScheduler == null) _lateScheduler = new LateMonoRunner("StandardLateRunner");
            return _lateScheduler;
        } }
        public static IRunner updateScheduler { get { if (_updateScheduler == null) _updateScheduler = new UpdateMonoRunner("StandardMonoRunner");
            return _updateScheduler;
        } }
#endif

        //physicScheduler -> updateScheduler -> coroutineScheduler -> lateScheduler

        internal static void KillSchedulers()
        {
            if (_multiThreadScheduler != null)
                _multiThreadScheduler.Dispose();
            _multiThreadScheduler = null;
            
#if UNITY_5 || UNITY_5_3_OR_NEWER
            if (_coroutineScheduler != null)
                _coroutineScheduler.Dispose();
            if (_physicScheduler != null)
                _physicScheduler.Dispose();
            if (_lateScheduler != null)
                _lateScheduler.Dispose();
            if (_updateScheduler != null)
                _updateScheduler.Dispose();
            
            _coroutineScheduler = null;
            _physicScheduler = null;
            _lateScheduler = null;
            _updateScheduler = null;
#endif
        }
    }
}
