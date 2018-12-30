using System.Collections;
#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.Unity;
#endif

namespace Svelto.Tasks.ExtraLean
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner _multiThreadScheduler;

#if UNITY_5 || UNITY_5_3_OR_NEWER
        static ExtraLeanCoroutineMonoRunner<IEnumerator> _coroutineScheduler;
        static ExtraLeanUpdateMonoRunner<IEnumerator> _updateScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER && later        
        static PhysicMonoRunner      _physicScheduler;
        static LateMonoRunner        _lateScheduler;
        
        static EarlyUpdateMonoRunner _earlyScheduler;
#endif
#endif

        public static MultiThreadRunner multiThreadScheduler { get { if (_multiThreadScheduler == null) _multiThreadScheduler = new MultiThreadRunner("MultiThreadRunner", false);
            return _multiThreadScheduler;
        } }

#if UNITY_5 || UNITY_5_3_OR_NEWER
        internal static IRunner standardScheduler 
        { 
            get 
            { 
                return coroutineScheduler;
            } 
        }
        public static ExtraLeanCoroutineMonoRunner<IEnumerator> coroutineScheduler { get { if (_coroutineScheduler == null) _coroutineScheduler = new ExtraLeanCoroutineMonoRunner<IEnumerator>("StandardCoroutineRunner");
            return _coroutineScheduler;
        } }
        
        public static ExtraLeanUpdateMonoRunner<IEnumerator> updateScheduler { get { if (_updateScheduler == null) _updateScheduler = new ExtraLeanUpdateMonoRunner<IEnumerator>("StandardMonoRunner");
            return _updateScheduler;
        } }
#if UNITY_5 || UNITY_5_3_OR_NEWER && later        
        public static PhysicMonoRunner physicScheduler { get { if (_physicScheduler == null) _physicScheduler = new PhysicMonoRunner("StandardPhysicRunner");
            return _physicScheduler;
        } }
        public static LateMonoRunner lateScheduler { get { if (_lateScheduler == null) _lateScheduler = new LateMonoRunner("StandardLateRunner");
            return _lateScheduler;
        } }
        public static EarlyUpdateMonoRunner earlyScheduler { get { if (_earlyScheduler == null) _earlyScheduler = new EarlyUpdateMonoRunner("EarlyUpdateMonoRunner");
            return _earlyScheduler;
        } }
        

        internal static void StartYieldInstruction(this IEnumerator instruction)
        {
            _coroutineScheduler.StartYieldInstruction(instruction);
        }

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
            _updateScheduler    = null;
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
