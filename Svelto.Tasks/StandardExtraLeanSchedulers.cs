using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.ExtraLean
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner<IEnumerator>    _multiThreadScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER    
        static Unity.CoroutineMonoRunner<IEnumerator>  _coroutineScheduler;
        static Unity.UpdateMonoRunner<IEnumerator>     _updateScheduler;
#if later    
        static PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>      _physicScheduler;
        static LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>        _lateScheduler;       
        static EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> _earlyScheduler;
#endif
#endif
        public static MultiThreadRunner<IEnumerator> multiThreadScheduler =>
            _multiThreadScheduler ?? (_multiThreadScheduler = new MultiThreadRunner<IEnumerator
                                          >("StandardMultiThreadRunner", false));

#if UNITY_5 || UNITY_5_3_OR_NEWER        
        internal static IRunner standardScheduler => updateScheduler;

        public static Unity.CoroutineMonoRunner<IEnumerator> coroutineScheduler =>
            _coroutineScheduler ?? (_coroutineScheduler = new Unity.CoroutineMonoRunner<IEnumerator>("StandardCoroutineRunner"));

        public static Unity.UpdateMonoRunner<IEnumerator> updateScheduler =>
            _updateScheduler ?? (_updateScheduler =
                                     new Unity.UpdateMonoRunner<IEnumerator>("StandardUpdateRunner"));
#if later        
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
