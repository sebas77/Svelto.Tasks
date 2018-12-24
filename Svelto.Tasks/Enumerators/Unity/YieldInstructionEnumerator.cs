#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
{
    public class YieldInstructionEnumerator : IEnumerator<TaskContract>
    {
        public YieldInstructionEnumerator(YieldInstruction instruction)
        {
            _instruction = instruction;

//            GetEnumerator().StartYieldInstruction();
        }

        public IEnumerator GetEnumerator()
        {
            yield return _instruction;

            _isDone = true;
        }
        
        public bool MoveNext()
        {
            return _isDone == false;
        }

        public void Reset()
        {
            _isDone = false;
        }

        TaskContract IEnumerator<TaskContract>.Current
        {
            get { return Yield.It; }
        }

        bool             _isDone;
        readonly YieldInstruction _instruction;

        public object Current
        {
            get { return null; }
        }

        public void Dispose()
        {}
    }
}
#endif