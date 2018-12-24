using System.Collections.Generic;
using Svelto.Tasks.Internal;

#if UNITY_5 || UNITY_5_3_OR_NEWER && later
using Svelto.Tasks.Unity;
#endif

namespace Svelto.Tasks
{
    public static class StandardTaskRoutineSchedulers
    {
        static MultiThreadRunner<TaskRoutine<IEnumerator<TaskContract>>> _multiThreadScheduler;
#if UNITY_5 || UNITY_5_3_OR_NEWER && later
        static CoroutineMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>   _coroutineScheduler;
        static PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>      _physicScheduler;
        static LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>        _lateScheduler;
        static UpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>      _updateScheduler;
        static EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> _earlyScheduler;
#endif
        public static IRunner multiThreadScheduler { get { if (_multiThreadScheduler == null) _multiThreadScheduler = new MultiThreadRunner<TaskRoutine<IEnumerator<TaskContract>>>("MultiThreadRunner", false);
            return _multiThreadScheduler;
        } }
        
#if UNITY_5 || UNITY_5_3_OR_NEWER && later
        internal static IRunner standardScheduler 
        { 
            get 
            { 
                return coroutineScheduler;
            } 
        }
        public static CoroutineMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> coroutineScheduler { get { if (_coroutineScheduler == null) _coroutineScheduler = new CoroutineMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardCoroutineRunner");
            return _coroutineScheduler;
        } }
        public static PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> physicScheduler { get { if (_physicScheduler == null) _physicScheduler = new PhysicMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardPhysicRunner");
            return _physicScheduler;
        } }
        public static LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> lateScheduler { get { if (_lateScheduler == null) _lateScheduler = new LateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardLateRunner");
            return _lateScheduler;
        } }
        public static EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> earlyScheduler { get { if (_earlyScheduler == null) _earlyScheduler = new EarlyUpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("EarlyUpdateMonoRunner");
            return _earlyScheduler;
        } }
        public static UpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>> updateScheduler { get { if (_updateScheduler == null) _updateScheduler = new UpdateMonoRunner<TaskRoutine<IEnumerator<TaskContract>>>("StandardMonoRunner");
            return _updateScheduler;
        } }

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
            
#if UNITY_5 || UNITY_5_3_OR_NEWER && later
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
            _earlyScheduler = null;
#endif
        }
    }
}
