#if UNITY_2017_2_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace Svelto.Tasks.Enumerators
{
    public class UnityWebRequestEnumerator : IEnumerator<TaskContract>
    {
        public UnityWebRequestEnumerator(UnityWebRequest www, int timeOutInSeconds = -1)
        {
            _www         = www;
            _www.timeout = timeOutInSeconds;

                _www.SendWebRequest();
        }

        public bool MoveNext()
        {
            return _www.isDone == false;
        }

        public void Reset()
        { }

        object IEnumerator.Current => _www;

        TaskContract IEnumerator<TaskContract>.Current => TaskContract.Yield.It;

        public UnityWebRequest Current => _www;

        public void Dispose()
        {
            _www.Dispose();
        }

        public UnityWebRequest www => _www;

        readonly UnityWebRequest _www;
    }
}
#endif