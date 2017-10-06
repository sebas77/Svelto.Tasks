using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Internal
{
    class RunnerBehaviourLate : MonoBehaviour
    {
        void LateUpdate()
        {
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        public void StartLateCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }

        IEnumerator _mainRoutine;
    }
}