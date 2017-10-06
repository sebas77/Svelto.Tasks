using System;
using System.Collections;

/// <summary>
/// CoroutineEx
///
/// Enable the TaskRunner to recognize and perform differently according what 
/// the wrapper IEnumerator returns.
/// </summary>

namespace Svelto.Tasks.Internal
{
    class CoroutineEx: IEnumerator
    {
        public object Current  { get { return _enumerator.Current; } }
        
        public CoroutineEx(IEnumerator enumerator):this()
        {
            if (enumerator is PausableTask || enumerator is TaskWrapper)
                throw new ArgumentException
                    ("Use of incompatible Enumerator, cannot be PausableTask or TaskWrapper");
            
            Reuse(enumerator);
        }

        public CoroutineEx()
        {
            _task = new SerialTaskCollection(1);
        }
        
        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }
        
        public void Reset()
        {
            throw new NotSupportedException();
        }

        public void Reuse(IEnumerator enumerator)
        {
           if (enumerator is CoroutineEx || enumerator is TaskCollection)
                _enumerator = enumerator;
            else
            {
                _task.Clear();
                _task.Add(enumerator);

                _enumerator = _task;
            }
        }

        IEnumerator		        _enumerator;
        SerialTaskCollection	_task;
    }
}

