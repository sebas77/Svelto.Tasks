#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Unity.Internal
{
    public class RunnerBehaviourUpdate : MonoBehaviour
    {
        public void Update()
        {
            if (_earlyMainRoutine != null)
                _earlyMainRoutine.MoveNext();
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        public void StartUpdateCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }
        
        public void StartEarlyUpdateCoroutine(IEnumerator enumerator)
        {
            _earlyMainRoutine = enumerator;
        }

        IEnumerator _earlyMainRoutine;
        IEnumerator _mainRoutine;
    }
}
#endif