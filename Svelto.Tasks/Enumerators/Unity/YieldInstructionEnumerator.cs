#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;

namespace Svelto.Tasks.Enumerators
{
    public class YieldInstructionEnumerator : IEnumerator<TaskContract>
    {
        public YieldInstructionEnumerator(YieldInstruction instruction)
        {
            _instruction = instruction;

            UnityCoroutineRunner.StartYieldCoroutine(GetEnumerator());
        }
        
        public YieldInstructionEnumerator(AsyncOperation instruction)
        {
            _instruction = instruction;

            UnityCoroutineRunner.StartYieldCoroutine(GetEnumerator());
        }

        IEnumerator GetEnumerator()
        {
            yield return _instruction;

            _isDone = true;
        }
        
        public bool MoveNext() { return _isDone == false; }
        public void Reset() { throw new NotSupportedException(); }

        public TaskContract Current => Yield.It;
        object IEnumerator.Current => null;
        
        bool                      _isDone;
        readonly YieldInstruction _instruction;

        public void Dispose() {}
    }
}
#endif