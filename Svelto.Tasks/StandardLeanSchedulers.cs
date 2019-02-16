using System.Collections.Generic;
using Svelto.Tasks.Unity;

#if UNITY_5 || UNITY_5_3_OR_NEWER && later
using Svelto.Tasks.Unity;
#endif

namespace Svelto.Tasks.Lean
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner<LeanSveltoTask<IEnumerator<TaskContract>>>    _multiThreadScheduler;

        static CoroutineMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>>  _coroutineScheduler;
        static UpdateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>>     _updateScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER && later    
        static PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>      _physicScheduler;
        static LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>        _lateScheduler;
        
        static EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> _earlyScheduler;
#endif
        public static MultiThreadRunner<LeanSveltoTask<IEnumerator<TaskContract>>> multiThreadScheduler =>
            _multiThreadScheduler ?? (_multiThreadScheduler =
                                          new MultiThreadRunner<LeanSveltoTask<IEnumerator<TaskContract>>
                                          >("StandardMultiThreadRunner", false));

        internal static IRunner standardScheduler => coroutineScheduler;

        public static CoroutineMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>> coroutineScheduler =>
            _coroutineScheduler ?? (_coroutineScheduler =
                                        new CoroutineMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>
                                        >("StandardCoroutineRunner"));

        public static UpdateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>> updateScheduler =>
            _updateScheduler ?? (_updateScheduler =
                                     new UpdateMonoRunner<LeanSveltoTask<IEnumerator<TaskContract>>
                                     >("StandardUpdateRunner"));
#if UNITY_5 || UNITY_5_3_OR_NEWER && later        
        public static PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> physicScheduler { get { if (_physicScheduler == null) _physicScheduler = new PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardPhysicRunner");
            return _physicScheduler;
        } }
        public static LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> lateScheduler { get { if (_lateScheduler == null) _lateScheduler = new LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardLateRunner");
            return _lateScheduler;
        } }
        public static EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> earlyScheduler { get { if (_earlyScheduler == null) _earlyScheduler = new EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("EarlyUpdateMonoRunner");
            return _earlyScheduler;
        } }
        internal static IRunner standardScheduler 
        { 
            get 
            { 
                return _multiThreadScheduler;
            } 
        }
#endif

        //physicScheduler -> earlyScheduler -> updateScheduler -> coroutineScheduler -> lateScheduler

        internal static void KillSchedulers()
        {
            if (_multiThreadScheduler != null && multiThreadScheduler.isKilled == false)
                _multiThreadScheduler.Dispose();
            _multiThreadScheduler = null;

            if (_coroutineScheduler != null)
                 _coroutineScheduler.Dispose();
            if (_updateScheduler != null)
                _updateScheduler.Dispose();
            
            _coroutineScheduler = null;
            _updateScheduler = null;
#if UNITY_5 || UNITY_5_3_OR_NEWER && later            
            if (_physicScheduler != null)
                _physicScheduler.Dispose();
            if (_lateScheduler != null)
                _lateScheduler.Dispose();
            
            _physicScheduler = null;
            _lateScheduler = null;
            _earlyScheduler = null;
#endif
        }

        public static void Pause()
        {
            if (_multiThreadScheduler != null && multiThreadScheduler.isKilled == false)
                _multiThreadScheduler.Pause();
#if UNITY_5 || UNITY_5_3_OR_NEWER
            if (_coroutineScheduler != null)
                _coroutineScheduler.Pause();
            if (_updateScheduler != null)
                _updateScheduler.Pause();
#endif            
        }
        
        public static void Resume()
        {
            if (_multiThreadScheduler != null && multiThreadScheduler.isKilled == false)
                _multiThreadScheduler.Resume();
#if UNITY_5 || UNITY_5_3_OR_NEWER
            if (_coroutineScheduler != null)
                _coroutineScheduler.Resume();
            if (_updateScheduler != null)
                _updateScheduler.Resume();
#endif            
        }
    }
}
