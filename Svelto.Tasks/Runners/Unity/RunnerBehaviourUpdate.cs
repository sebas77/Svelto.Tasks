#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Unity.Internal
{
    public class RunnerBehaviourUpdate : MonoBehaviour
    {
        public void Update()
        {
#if DO_IT_PROPERLY_WHEN_I_MERGE_GAME_OBJECTS            
            if (_earlyMainRoutine != null)
                _earlyMainRoutine.MoveNext();
#endif    
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        public void StartUpdateCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }
#if DO_IT_PROPERLY_WHEN_I_MERGE_GAME_OBJECTS        
        public void StartEarlyUpdateCoroutine(IEnumerator enumerator)
        {
            _earlyMainRoutine = enumerator;
        }

        IEnumerator _earlyMainRoutine;
#endif    
        IEnumerator _mainRoutine;
    }
}
#endif