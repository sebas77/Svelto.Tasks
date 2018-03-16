#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Internal
{
    class RunnerBehaviourPhysic : MonoBehaviour
    {
        void FixedUpdate()
        {
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        public void StartPhysicCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }

        IEnumerator _mainRoutine;
    }
}
#endif