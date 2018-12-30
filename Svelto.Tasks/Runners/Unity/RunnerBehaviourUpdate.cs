#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
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
                ExecuteRoutines(_coroutineProcesses);
                
                yield return _waitForEndOfFrame;
                
                ExecuteRoutines(_endOfFrameRoutines);
            }
        }
        
        public void Update()
        {
            ExecuteRoutines(_earlyProcesses);
            ExecuteRoutines(_updateProcesses);
        }

        static void ExecuteRoutines(FasterListThreadSafe<IProcessSveltoTasks> list)
        {
            int count;
            var routines = list.ToArrayFast(out count);

            for (int i = 0; i < count; i++)
            {
                var ret = routines[i].MoveNext(false);
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
            ExecuteRoutines(_lateRoutines);
        }

        public void StartLateCoroutine(IProcessSveltoTasks enumerator)
        {
            _lateRoutines.Add(enumerator);
        }
        
        void FixedUpdate()
        {
            ExecuteRoutines(_physicRoutines);
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
