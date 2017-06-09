#if UNITY_5
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Internal
{
    class RunnerBehaviour : MonoBehaviour
    {}

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
