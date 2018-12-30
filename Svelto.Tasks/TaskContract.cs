using System.Collections;
using System.Runtime.InteropServices;

namespace Svelto.Tasks
{
    public struct TaskContract
    {
        TaskContract(int number) : this()
        {
            _currentState      = states.value;
            _returnValue.int32 = number;
        }

        TaskContract(ContinuationEnumerator continuation) : this()
        {
            _currentState               = states.continuation;
            _returnObjects.continuation = continuation;
        }

        public TaskContract(IEnumerator enumerator) : this()
        {
            _currentState            = states.enumerator;
            _returnObjects.reference = enumerator;
        }

        public TaskContract(Break breakit) : this()
        {
            _currentState          = states.breakit;
            _returnObjects.breakIt = breakit;
        }

        public TaskContract(float val) : this()
        {
            _currentState       = states.value;
            _returnValue.single = val;
        }

        public TaskContract(object val) : this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
        }

        public TaskContract(string val) : this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct fieldValues
        {
            [FieldOffset(0)] internal float single;
            [FieldOffset(0)] internal int   int32;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct fieldObjects
        {
            [FieldOffset(0)] internal object              reference;
            [FieldOffset(0)] internal Break               breakIt;
            [FieldOffset(0)] internal ContinuationEnumerator continuation;
        }

        public static implicit operator TaskContract(int number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(float number)
        {
            return new TaskContract(number);
        }

        public static implicit operator TaskContract(long number)
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
            return new TaskContract();
        }

        public int ToInt()
        {
            DBC.Tasks.Check.Require(_currentState == states.value, "invalid state");
            
            return _returnValue.int32;
        }
        
        public Break breakit
        {
            get { return _currentState == states.breakit ?_returnObjects.breakIt : null; }
        }


        public IEnumerator enumerator
        {
            get { return _currentState == states.enumerator ? (IEnumerator) _returnObjects.reference : null; }
        }

        public ContinuationEnumerator ContinuationEnumerator
        {
            get { return _currentState == states.continuation ? _returnObjects.continuation : null; }
        }

        public object reference
        {
            get { return _currentState == states.value ? _returnObjects.reference : null; }
        }

        public bool yieldIt
        {
            get { return _currentState == states.yieldit; }
        }

        readonly fieldValues  _returnValue;
        readonly fieldObjects _returnObjects;
        readonly states       _currentState;
        

        enum states
        {
            yieldit = 0,
            value,
            continuation,
            breakit,
            enumerator
        }
    }
}
