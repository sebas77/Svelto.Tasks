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

        static void ExecuteRoutines(FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> list, PlatformProfiler profiler)
        {
            var orderedRoutines = list.ToArrayFast(out var orderedCount);

            for (int ii = 0; ii < orderedCount; ii++)
            {
                if (orderedRoutines[ii] == null)
                    continue;

                var routines = orderedRoutines[ii].ToArrayFast(out var count);
                for (int i = 0; i < count; i++)
                {
                    var ret = routines[i].MoveNext(false, profiler);
                    if (ret == false)
                    {
                        orderedRoutines[ii].UnorderedRemoveAt(i);
                        count--;
                        i--;
                    }
                }
            }
        }

        public void StartSveltoCoroutine(IProcessSveltoTasks process, uint runningOrder)
        {
            if (_coroutineProcesses.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _coroutineProcesses.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _coroutineProcesses[(int)runningOrder].Add(process);
        }

        public void StartUpdateCoroutine(IProcessSveltoTasks enumerator, uint runningOrder)
        {
            if (_updateProcesses.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _updateProcesses.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _updateProcesses[(int)runningOrder].Add(enumerator);
        }

        public void StartEarlyUpdateCoroutine(IProcessSveltoTasks enumerator, uint runningOrder)
        {
            if (_earlyProcesses.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _earlyProcesses.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _earlyProcesses[(int)runningOrder].Add(enumerator);
        }

        public void StartEndOfFrameCoroutine(IProcessSveltoTasks enumerator, uint runningOrder)
        {
            if (_endOfFrameRoutines.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _endOfFrameRoutines.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _endOfFrameRoutines[(int)runningOrder].Add(enumerator);
        }

        void LateUpdate()
        {
            using (var platform = new PlatformProfiler("late tasks")) ExecuteRoutines(_lateRoutines, platform);
        }

        public void StartLateCoroutine(IProcessSveltoTasks enumerator, uint runningOrder)
        {
            if (_lateRoutines.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _lateRoutines.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _lateRoutines[(int)runningOrder].Add(enumerator);
        }

        void FixedUpdate()
        {
            using (var platform = new PlatformProfiler("physic tasks")) ExecuteRoutines(_physicRoutines, platform);
        }

        public void StartPhysicCoroutine(IProcessSveltoTasks enumerator, uint runningOrder)
        {
            if (_physicRoutines.Count <= runningOrder || _updateProcesses[(int)runningOrder] == null)
                _physicRoutines.Add(runningOrder, new FasterListThreadSafe<IProcessSveltoTasks>());
            _physicRoutines[(int)runningOrder].Add(enumerator);
        }

        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _earlyProcesses     = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _endOfFrameRoutines = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _updateProcesses    = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _lateRoutines       = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _physicRoutines     = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
        readonly FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>> _coroutineProcesses = new FasterListThreadSafe<FasterListThreadSafe<IProcessSveltoTasks>>();
    }
}
#endif
