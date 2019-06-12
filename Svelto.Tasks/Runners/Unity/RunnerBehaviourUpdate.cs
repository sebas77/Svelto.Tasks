#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;
using UnityEngine;

namespace Svelto.Tasks.Unity.Internal
{
    class RunnerBehaviourUpdate : MonoBehaviour
    {
        void Awake()
        {
            StartCoroutine(CoroutineProcess());
        }

        IEnumerator CoroutineProcess()
        {
            while (true)
            {
                using (var platform = new PlatformProfiler("coroutine tasks")) ExecuteRoutines(_coroutineProcesses, platform);
                
                yield return _waitForEndOfFrame;
            
                using (var platform = new PlatformProfiler("endOfFrame tasks")) ExecuteRoutines(_endOfFrameRoutines, platform);
            }
        }
        
        public void Update()
        {
            using (var platform = new PlatformProfiler("early tasks")) ExecuteRoutines(_earlyProcesses, platform);
            using (var platform = new PlatformProfiler("update tasks")) ExecuteRoutines(_updateProcesses, platform);
        }

        static void ExecuteRoutines(FasterListThreadSafe<IProcessSveltoTasks> list, PlatformProfiler profiler)
        {
            var routines = list.ToArrayFast(out var count);

            for (int i = 0; i < count; i++)
            {
                var ret = routines[i].MoveNext(false, profiler);
                if (ret == false)
                {
                    list.UnorderedRemoveAt(i);
                    count--;
                    i--;
                }
            }
        }
        
        public void StartSveltoCoroutine(IProcessSveltoTasks process)
        {
            _coroutineProcesses.Add(process);
        }

        public void StartUpdateCoroutine(IProcessSveltoTasks enumerator)
        {
            _updateProcesses.Add(enumerator);
        }
        
        public void StartEarlyUpdateCoroutine(IProcessSveltoTasks enumerator)
        {
            _earlyProcesses.Add(enumerator);
        }
        
        public void StartEndOfFrameCoroutine(IProcessSveltoTasks enumerator)
        {
            _endOfFrameRoutines.Add(enumerator);
        }

        void LateUpdate()
        {
            using (var platform = new PlatformProfiler("late tasks")) ExecuteRoutines(_lateRoutines, platform);
        }

        public void StartLateCoroutine(IProcessSveltoTasks enumerator)
        {
            _lateRoutines.Add(enumerator);
        }
        
        void FixedUpdate()
        {
            using (var platform = new PlatformProfiler("physic tasks")) ExecuteRoutines(_physicRoutines, platform);
        }

        public void StartPhysicCoroutine(IProcessSveltoTasks enumerator)
        {
            _physicRoutines.Add(enumerator);
        }

        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        readonly FasterListThreadSafe<IProcessSveltoTasks> _earlyProcesses     = new FasterListThreadSafe<IProcessSveltoTasks>();
        readonly FasterListThreadSafe<IProcessSveltoTasks> _endOfFrameRoutines = new FasterListThreadSafe<IProcessSveltoTasks>();
        readonly FasterListThreadSafe<IProcessSveltoTasks> _updateProcesses    = new FasterListThreadSafe<IProcessSveltoTasks>();
        readonly FasterListThreadSafe<IProcessSveltoTasks> _lateRoutines       = new FasterListThreadSafe<IProcessSveltoTasks>();
        readonly FasterListThreadSafe<IProcessSveltoTasks> _physicRoutines     = new FasterListThreadSafe<IProcessSveltoTasks>();
        readonly FasterListThreadSafe<IProcessSveltoTasks> _coroutineProcesses = new FasterListThreadSafe<IProcessSveltoTasks>();
    }
}
#endif
