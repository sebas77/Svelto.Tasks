using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks
{
    public struct TaskContract
    {
        internal TaskContract(int number) : this()
        {
            _currentState      = states.value;
            _returnValue.int32 = number;
        }

        internal TaskContract(ulong number) : this()
        {
            _currentState       = states.value;
            _returnValue.uint64 = number;
        }

        internal TaskContract(ContinuationEnumerator continuation) : this()
        {
            _currentState = states.continuation;
            _continuation = continuation;
        }

        internal TaskContract(IEnumerator<TaskContract> enumerator) : this()
        {
            _currentState            = states.leanEnumerator;
            _returnObjects.reference = enumerator;
        }
        
        internal TaskContract(IEnumerator enumerator) : this()
        {
            _currentState            = states.extraLeanEnumerator;
            _returnObjects.reference = enumerator;
        }

        TaskContract(Break breakit) : this()
        {
            _currentState          = states.breakit;
            _returnObjects.breakIt = breakit;
        }

        TaskContract(Yield yieldIt) : this()
        {
            _currentState = states.yieldit;
        }

        TaskContract(float val) : this()
        {
            _currentState       = states.value;
            _returnValue.single = val;
        }

        TaskContract(string val) : this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
        }
        
        public TaskContract(object o) : this()
        {
            _currentState          = states.reference;
            _returnObjects.reference = o;
        }

        public static implicit operator TaskContract(int number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(ulong number)
        {
            return new TaskContract(number);
        }
        
        public static implicit operator TaskContract(float number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(ContinuationEnumerator continuation)
        {
            return new TaskContract(continuation);
        }

        public static implicit operator TaskContract(Break breakit)
        {
            return new TaskContract(breakit);
        }

        public static implicit operator TaskContract(Yield yieldit)
        {
            return new TaskContract(yieldit);
        }
        
        public static implicit operator TaskContract(string payload)
        {
            return new TaskContract(payload);
        }
        
        public int ToInt()
        {
            return _returnValue.int32;
        }

        public ulong ToUlong()
        {
            return _returnValue.uint64;
        }

        public uint ToUInt() { return _returnValue.uint32; }
        
        public float ToFloat()
        {
            return _returnValue.single;
        }

        public T ToRef<T>() where T : class
        {
            return _returnObjects.reference as T;
        }
        
        internal Break breakIt => _currentState == states.breakit ? _returnObjects.breakIt : null;

        internal IEnumerator enumerator => _currentState == states.leanEnumerator || 
            _currentState == states.extraLeanEnumerator ? (IEnumerator) _returnObjects.reference : null;

        internal ContinuationEnumerator? Continuation
        {
            get
            {
                if (_currentState != states.continuation)
                    return null;

                return _continuation;
            }
        }
        
        internal bool isTaskEnumerator => _currentState == states.leanEnumerator;
        internal object reference => _currentState == states.value ? _returnObjects.reference : null;
        internal bool hasValue => _currentState == states.value;
        internal bool yieldIt => _currentState == states.yieldit;
        
        readonly FieldValues            _returnValue;
        readonly FieldObjects           _returnObjects;
        readonly states                 _currentState;
        readonly ContinuationEnumerator _continuation;
        
        [StructLayout(LayoutKind.Explicit)]
        struct FieldValues
        {
            [FieldOffset(0)] internal float single;
            [FieldOffset(0)] internal int   int32;
            [FieldOffset(0)] internal uint  uint32;
            [FieldOffset(0)] internal ulong uint64;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FieldObjects
        {
            [FieldOffset(0)] internal object reference;
            [FieldOffset(0)] internal Break  breakIt;
        }

        enum states
        {
            yieldit = 0,
            value,
            continuation,
            breakit,
            leanEnumerator,
            extraLeanEnumerator,
            reference
        }
    }
}