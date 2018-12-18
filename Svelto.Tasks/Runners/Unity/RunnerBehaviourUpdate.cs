#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;

namespace Svelto.Tasks.Unity.Internal
{
    class RunnerBehaviourUpdate : MonoBehaviour
    {
        public void Update()
        {
            ExecuteRoutines(_earlyProcesses);
            ExecuteRoutines(_updateProcesses);
        }

        static void ExecuteRoutines(FasterList<IProcessSveltoTasks> list)
        {
            var count    = list.Count;
            var routines = list.ToArrayFast();

            for (int i = 0; i < count; i++)
            {
                var ret = routines[i].MoveNext();
                if (ret == false)
                {
                    list.UnorderedRemoveAt(i);
                    count--;
                    i--;
                }
            }
        }

        public void StartUpdateCoroutine(IProcessSveltoTasks enumerator)
        {
            _updateProcesses.Add(enumerator);
        }
        
        public void StartEarlyUpdateCoroutine(IProcessSveltoTasks enumerator)
        {
            _earlyProcesses.Add(enumerator);
        }
        
        void Awake()
        {
            StartCoroutine(ExecuteEndOfFrameProcesses());
        }
        
        public void StartEndOfFrameCoroutine(IProcessSveltoTasks enumerator)
        {
            _endOfFrameRoutines.Add(enumerator);
        }

        IEnumerator ExecuteEndOfFrameProcesses()
        {
            while (true)
            {
                yield return _waitForEndOfFrame;

                ExecuteRoutines(_earlyProcesses);
            }
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

        readonly FasterList<IProcessSveltoTasks> _earlyProcesses     = new FasterList<IProcessSveltoTasks>();
        readonly FasterList<IProcessSveltoTasks> _endOfFrameRoutines = new FasterList<IProcessSveltoTasks>();
        readonly FasterList<IProcessSveltoTasks> _updateProcesses    = new FasterList<IProcessSveltoTasks>();
        readonly FasterList<IProcessSveltoTasks> _lateRoutines       = new FasterList<IProcessSveltoTasks>();
        readonly FasterList<IProcessSveltoTasks> _physicRoutines     = new FasterList<IProcessSveltoTasks>();
    }
}
#endif