#if (UNITY_5 || UNITY_5_3_OR_NEWER) && ENABLE_LEGACY_WWWWW
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
{
    public class WWWEnumerator : IEnumerator<WWW>
    {
        public WWWEnumerator(WWW www, float timeOut = -1)
        {
            _www = www;
            _timeOut = timeOut;
            _then = DateTime.Now;
        }

        public bool MoveNext()
        {
            var timePassed = (float)(DateTime.Now - _then).TotalSeconds;

            if (_timeOut > 0.0f && timePassed > _timeOut)
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

        readonly WWW      _www;
        readonly float    _timeOut;
        readonly DateTime _then;
    }
}
#endif