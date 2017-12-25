#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Internal
{
    class RunnerBehaviourUpdate : MonoBehaviour
    {
        void Update()
        {
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        public void StartUpdateCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }

        IEnumerator _mainRoutine;
    }
}
#endif