using System.Collections;

namespace Svelto.Tasks.ExtraLean
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner<IEnumerator> _multiThreadScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER
        static Unity.CoroutineMonoRunner<IEnumerator>   _coroutineScheduler;
        static Unity.UpdateMonoRunner<IEnumerator>      _updateScheduler;
        static Unity.PhysicMonoRunner<IEnumerator>      _physicScheduler;
        static Unity.LateMonoRunner<IEnumerator>        _lateScheduler;
        static Unity.EarlyUpdateMonoRunner<IEnumerator> _earlyScheduler;

#endif
        public static MultiThreadRunner<IEnumerator> multiThreadScheduler => _multiThreadScheduler ??
            (_multiThreadScheduler = new MultiThreadRunner<IEnumerator>("StandardMultiThreadRunner", false));

#if UNITY_5 || UNITY_5_3_OR_NEWER
        internal static IRunner standardScheduler => updateScheduler;

        public static Unity.CoroutineMonoRunner<IEnumerator> coroutineScheduler => _coroutineScheduler ??
            (_coroutineScheduler = new Unity.CoroutineMonoRunner<IEnumerator>("StandardCoroutineRunner"));

        public static Unity.UpdateMonoRunner<IEnumerator> updateScheduler => _updateScheduler ??
            (_updateScheduler = new Unity.UpdateMonoRunner<IEnumerator>("StandardUpdateRunner"));

        public static Unity.PhysicMonoRunner<IEnumerator> physicScheduler
        {
            get
            {
                if (_physicScheduler == null)
                    _physicScheduler = new Unity.PhysicMonoRunner<IEnumerator>("StandardPhysicRunner");

                return _physicScheduler;
            }
        }

        public static Unity.LateMonoRunner<IEnumerator> lateScheduler
        {
            get
            {
                if (_lateScheduler == null)
                    _lateScheduler = new Unity.LateMonoRunner<IEnumerator>("StandardLateRunner");

                return _lateScheduler;
            }
        }

        public static Unity.EarlyUpdateMonoRunner<IEnumerator> earlyScheduler
        {
            get
            {
                if (_earlyScheduler == null)
                    _earlyScheduler = new Unity.EarlyUpdateMonoRunner<IEnumerator>("EarlyUpdateMonoRunner");
                return _earlyScheduler;
            }
        }
#else
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
#if UNITY_5 || UNITY_5_3_OR_NEWER
            _coroutineScheduler?.Dispose();
            _updateScheduler?.Dispose();
            _physicScheduler?.Dispose();
            _lateScheduler?.Dispose();
            _earlyScheduler?.Dispose();

            _coroutineScheduler = null;
            _updateScheduler = null;
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
            _coroutineScheduler?.Pause();
            _updateScheduler?.Pause();
            _physicScheduler?.Pause();
            _lateScheduler?.Pause();
            _earlyScheduler?.Pause();
#endif
        }

        public static void Resume()
        {
            if (_multiThreadScheduler != null && multiThreadScheduler.isKilled == false)
                _multiThreadScheduler.Resume();
#if UNITY_5 || UNITY_5_3_OR_NEWER
            _coroutineScheduler?.Resume();
            _updateScheduler?.Resume();
            _physicScheduler?.Resume();
            _lateScheduler?.Resume();
            _earlyScheduler?.Resume();
#endif
        }

        public static void StopAllCoroutines()
        {
            if (_multiThreadScheduler != null && multiThreadScheduler.isKilled == false)
                _multiThreadScheduler.Stop();
            
#if UNITY_5 || UNITY_5_3_OR_NEWER            
            _coroutineScheduler?.Stop();
            _updateScheduler?.Stop();
            _physicScheduler?.Stop();
            _lateScheduler?.Stop();
            _earlyScheduler?.Stop();
#endif
        }
    }
}