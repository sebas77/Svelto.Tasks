using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Svelto.Tasks
{
    public class WWWEnumerator : IEnumerator<WWW>
    {
        public WWWEnumerator(WWW www, float timeOut = -1)
        {
            _www = www;
            _timeOut = timeOut;
            _timePassed = 0;
        }

        public bool MoveNext()
        {
            _timePassed += Time.deltaTime;

            if (_timeOut > 0.0f && _timePassed > _timeOut)
                return false;

            var result = _www.isDone == false;

            return result;
        }

        public void Reset()
        {}

        object IEnumerator.Current
        {
            get { return _www; }
        }

        public WWW Current
        {
            get { return _www; }
        }

        public void Dispose()
        {
            _www.Dispose();
        }

        public WWW www { get { return _www; }}

        WWW     _www;
        float   _timeOut;
        float   _timePassed;
    }
}
    
