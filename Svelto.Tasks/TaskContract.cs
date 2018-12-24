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
            HasValue           = true;
        }

        TaskContract(ContinuationWrapper continuation) : this()
        {
            _currentState               = states.continuation;
            _returnObjects.continuation = continuation;
            HasValue                    = true;
        }

        public TaskContract(IEnumerator enumerator) : this()
        {
            _currentState            = states.enumerator;
            _returnObjects.reference = enumerator;
            HasValue                 = true;
        }

        public TaskContract(Break breakit) : this()
        {
            _currentState          = states.breakit;
            _returnObjects.breakIt = breakit;
            HasValue               = true;
        }

        public TaskContract(float val) : this()
        {
            _currentState       = states.value;
            _returnValue.single = val;
            HasValue            = true;
        }

        public TaskContract(object val) : this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
            HasValue                 = true;
        }

        public TaskContract(string val) : this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
            HasValue                 = true;
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
            [FieldOffset(0)] internal ContinuationWrapper continuation;
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

        public static implicit operator TaskContract(ContinuationWrapper continuation)
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

/*        public static explicit operator int(TaskContract contract)
        {
            DBC.Tasks.Check.Require(contract.HasValue, "invalid state");
            DBC.Tasks.Check.Require(contract._currentState == states.value, "invalid state");

            return contract._returnValue.int32;
        }*/

        public static explicit operator Break(TaskContract contract)
        {
            return contract._currentState == states.breakit
                       ? contract._returnObjects.breakIt
                       : null;
        }
        
        public Break breakit
        {
            get { return _currentState == states.breakit ?_returnObjects.breakIt : null; }
        }


        public IEnumerator enumerator
        {
            get { return _currentState == states.enumerator ? (IEnumerator) _returnObjects.reference : null; }
        }

        public ContinuationWrapper continuationWrapper
        {
            get { return _currentState == states.continuation ? _returnObjects.continuation : null; }
        }

        public object reference
        {
            get { return _currentState == states.value ? _returnObjects.reference : null; }
        }

        public bool HasValue { get; private set; }

        readonly fieldValues  _returnValue;
        readonly fieldObjects _returnObjects;
        readonly states       _currentState;
        
        enum states
        {
            value,
            continuation,
            breakit,
            enumerator
        }
    }
}
