using System.Collections.Generic;

namespace Svelto.Tasks.Lean
{
    public static class StandardSchedulers
    {
        static MultiThreadRunner<IEnumerator<TaskContract>> _multiThreadScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER
        static Unity.CoroutineMonoRunner<IEnumerator<TaskContract>> _coroutineScheduler;
        static Unity.UpdateMonoRunner<IEnumerator<TaskContract>>    _updateScheduler;
        static Unity.PhysicMonoRunner<IEnumerator<TaskContract>>    _physicScheduler;
        static Unity.LateMonoRunner<IEnumerator<TaskContract>>      _lateScheduler;
        static Unity.EarlyUpdateMonoRunner<IEnumerator<TaskContract>>     _earlyScheduler;
#endif
        public static MultiThreadRunner<IEnumerator<TaskContract>> multiThreadScheduler => _multiThreadScheduler ??
            (_multiThreadScheduler =
                new MultiThreadRunner<IEnumerator<TaskContract>>("StandardMultiThreadRunner", false));

#if UNITY_5 || UNITY_5_3_OR_NEWER
        internal static IRunner standardScheduler => updateScheduler;

        public static Unity.CoroutineMonoRunner<IEnumerator<TaskContract>> coroutineScheduler => _coroutineScheduler ??
            (_coroutineScheduler = new Unity.CoroutineMonoRunner<IEnumerator<TaskContract>>("StandardCoroutineRunner"));

        public static Unity.UpdateMonoRunner<IEnumerator<TaskContract>> updateScheduler => _updateScheduler ??
            (_updateScheduler = new Unity.UpdateMonoRunner<IEnumerator<TaskContract>>("StandardUpdateRunner"));

        public static Unity.PhysicMonoRunner<IEnumerator<TaskContract>> physicScheduler
        {
            get
            {
                if (_physicScheduler == null)
                    _physicScheduler = new Unity.PhysicMonoRunner<IEnumerator<TaskContract>>("StandardPhysicRunner");
                return _physicScheduler;
            }
        }

        public static Unity.LateMonoRunner<IEnumerator<TaskContract>> lateScheduler
        {
            get
            {
                if (_lateScheduler == null)
                    _lateScheduler = new Unity.LateMonoRunner<IEnumerator<TaskContract>>("StandardLateRunner");
                return _lateScheduler;
            }
        }

        public static Unity.EarlyUpdateMonoRunner<IEnumerator<TaskContract>> earlyScheduler
        {
            get
            {
                if (_earlyScheduler == null)
                    _earlyScheduler = new Unity.EarlyUpdateMonoRunner<IEnumerator<TaskContract>>("EarlyUpdateMonoRunner");
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

        internal static void Dispose()
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

        public static void Stop()
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