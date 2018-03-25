#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Internal
{
    class RunnerBehaviourEndOfFrame : MonoBehaviour
    {
        void Awake()
        {
            StartCoroutine(WaitForEndOfFrameLoop());
        }
        
        public void StartEndOfFrameCoroutine(IEnumerator enumerator)
        {
            _mainRoutine = enumerator;
        }

        IEnumerator WaitForEndOfFrameLoop()
        {
            yield return _waitForEndOfFrame;
            
            if (_mainRoutine != null)
                _mainRoutine.MoveNext();
        }

        IEnumerator       _mainRoutine;
        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    }
}
#endif