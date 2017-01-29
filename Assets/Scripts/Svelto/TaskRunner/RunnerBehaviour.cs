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

        public void StartCoroutinePhysic(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }

        IEnumerator _mainRoutine;
    }
}
