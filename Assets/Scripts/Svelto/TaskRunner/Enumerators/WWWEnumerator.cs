using UnityEngine;

namespace Svelto.Tasks
{
    public class WWWEnumerator : ParallelEnumerator
    {
        public WWWEnumerator(WWW www)
        {
            _www = www;
        }

        public object Current { get { return _www; } }

        public bool MoveNext()
        {
            return _www.isDone == false;
        }

        public void Reset()
        {
        }

        WWW _www;
    }
}
    
