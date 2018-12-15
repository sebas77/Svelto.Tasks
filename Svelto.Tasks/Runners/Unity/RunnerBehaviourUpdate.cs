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
            ExecuteRoutines(_earlyMainRoutines);
            ExecuteRoutines(_mainRoutines);
        }

        static void ExecuteRoutines(FasterList<IEnumerator> list)
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

        public void StartUpdateCoroutine(IEnumerator enumerator)
        {
            _mainRoutines.Add(enumerator);
        }
        
        public void StartEarlyUpdateCoroutine(IEnumerator enumerator)
        {
            _earlyMainRoutines.Add(enumerator);
        }
        
        void Awake()
        {
            StartCoroutine(WaitForEndOfFrameLoop());
        }
        
        public void StartEndOfFrameCoroutine(IEnumerator enumerator)
        {
            _endOfFrameRoutines.Add(enumerator);
        }

        IEnumerator WaitForEndOfFrameLoop()
        {
            while (true)
            {
                yield return _waitForEndOfFrame;

                ExecuteRoutines(_earlyMainRoutines);
            }
        }
        
        void LateUpdate()
        {
            ExecuteRoutines(_lateRoutines);
        }

        public void StartLateCoroutine(IEnumerator enumerator)
        {
            _lateRoutines.Add(enumerator);
        }
        
        void FixedUpdate()
        {
            ExecuteRoutines(_physicRoutines);
        }

        public void StartPhysicCoroutine(IEnumerator enumerator)
        {
            _physicRoutines.Add(enumerator);
        }

        IEnumerator _mainRoutine;

        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
        
        FasterList<IEnumerator> _earlyMainRoutines  = new FasterList<IEnumerator>();
        FasterList<IEnumerator> _endOfFrameRoutines = new FasterList<IEnumerator>();
        FasterList<IEnumerator> _mainRoutines       = new FasterList<IEnumerator>();
        FasterList<IEnumerator> _lateRoutines = new FasterList<IEnumerator>();
        FasterList<IEnumerator> _physicRoutines = new FasterList<IEnumerator>();
    }
}
#endif