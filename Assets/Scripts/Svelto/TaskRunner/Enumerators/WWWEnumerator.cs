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

            if (result == false)
                Utility.Console.Log(_www.url + " " + _www.progress * 100.0f + " time passed " + _timePassed);

            return result;

        }

        public void Reset()
        {}

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public WWW Current
        {
            get { return _www; }
        }

        public void Dispose()
        {
            _www.Dispose();
        }

        WWW     _www;
        float   _timeOut;
        float   _timePassed;
    }
}
    
