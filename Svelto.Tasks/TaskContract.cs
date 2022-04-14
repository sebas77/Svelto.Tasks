using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks
{
    public struct TaskContract
    {
        public TaskContract(int number) : this()
        {
            _currentState      = States.value;
            _returnValue.int32 = number;
        }

        public TaskContract(ulong number) : this()
        {
            _currentState       = States.value;
            _returnValue.uint64 = number;
        }

        public TaskContract(float val) : this()
        {
            _currentState       = States.value;
            _returnValue.single = val;
        }

        public TaskContract(uint val) : this()
        {
            _currentState       = States.value;
            _returnValue.uint32 = val;
        }
        
        public TaskContract(bool val) : this()
        {
            _currentState       = States.value;
            _returnValue.vbool = val;
        }

        public TaskContract(string val) : this()
        {
            _currentState            = States.value;
            _returnObjects.reference = val;
        }

        public TaskContract(object o) : this()
        {
            _currentState            = States.reference;
            _returnObjects.reference = o;
        }

        internal TaskContract(Continuation continuation) : this()
        {
            _currentState = States.continuation;
            _continuation = continuation;
        }

        internal TaskContract(IEnumerator<TaskContract> enumerator) : this()
        {
            DBC.Tasks.Check.Require(enumerator != null);
            _currentState            = States.leanEnumerator;
            _returnObjects.reference = enumerator;
        }

        internal TaskContract(IEnumerator enumerator) : this()
        {
            DBC.Tasks.Check.Require(enumerator != null);
            _currentState            = States.extraLeanEnumerator;
            _returnObjects.reference = enumerator;
        }

        TaskContract(Break breakit) : this()
        {
            _currentState          = States.breakit;
            _returnObjects.breakIt = breakit;
        }

        public static implicit operator TaskContract(int number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(ulong number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(long number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(float number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(Continuation continuation)
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

        public long ToLong()
        {
            return (long)_returnValue.uint64;
        }

        public uint ToUInt()
        {
            return _returnValue.uint32;
        }

        public float ToFloat()
        {
            return _returnValue.single;
        }
        
        public bool ToBool()
        {
            return _returnValue.vbool;
        }

        public T ToRef<T>() where T : class
        {
            return _returnObjects.reference as T;
        }

        internal Break breakIt => _currentState == States.breakit ? _returnObjects.breakIt : null;

        internal bool isExtraLeanEnumerator(out IEnumerator enumerator)
        {
            if (_currentState == States.extraLeanEnumerator)
            {
                enumerator = (IEnumerator)_returnObjects.reference;

                return true;
            }

            enumerator = null;

            return false;
        }

        internal bool isTaskEnumerator(out IEnumerator<TaskContract> enumerator)
        {
            if (_currentState == States.leanEnumerator)
            {
                enumerator = (IEnumerator<TaskContract>)_returnObjects.reference;

                return true;
            }

            enumerator = null;

            return false;
        }

        internal Continuation? continuation
        {
            get
            {
                if (_currentState != States.continuation)
                    return null;

                return _continuation;
            }
        }

        internal object reference => _currentState == States.value ? _returnObjects.reference : null;
        internal bool   hasValue  => _currentState == States.value;
        internal bool   yieldIt   => _currentState == States.yieldit;

        readonly FieldValues  _returnValue;
        readonly FieldObjects _returnObjects;
        readonly States       _currentState;
        readonly Continuation _continuation;

        [StructLayout(LayoutKind.Explicit)]
        struct FieldValues
        {
            [FieldOffset(0)] internal float single;
            [FieldOffset(0)] internal int   int32;
            [FieldOffset(0)] internal uint  uint32;
            [FieldOffset(0)] internal ulong uint64;
            [FieldOffset(0)] internal bool  vbool;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FieldObjects
        {
            [FieldOffset(0)] internal object reference;
            [FieldOffset(0)] internal Break  breakIt;
        }

        enum States
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