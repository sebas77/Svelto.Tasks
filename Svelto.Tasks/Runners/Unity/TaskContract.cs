using System.Collections;
using System.Runtime.InteropServices;

namespace Svelto.Tasks.Unity
{
    public struct TaskContract
    {
        TaskContract(int number):this()
        {
            _currentState = states.value;
            _returnValue.int32 = number;
        }
        
        TaskContract(ContinuationWrapper continuation):this()
        {
            _currentState = states.continuation;
            _returnObjects.continuation = continuation;
        }
        
        public TaskContract(IEnumerator enumerator):this()
        {
            _currentState = states.enumerator;
            _returnObjects.enumerator = enumerator;
        }

        public TaskContract(Break breakit):this()
        {
            _currentState         = states.breakit;
            _returnObjects.breakIt  = breakit;
        }
        
        public TaskContract(float val):this()
        {
            _currentState        = states.value;
            _returnValue.single = val;
        }
        
        public TaskContract(object val):this()
        {
            _currentState       = states.value;
            _returnObjects.reference = val;
        }
        
        public TaskContract(long val):this()
        {
            _currentState       = states.value;
            _returnValue.int64  = val;
        }
        
        public TaskContract(string val):this()
        {
            _currentState            = states.value;
            _returnObjects.reference = val;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct fieldValues
        {
            [FieldOffset(0)] internal float               single;
            [FieldOffset(0)] internal int                 int32; 
            [FieldOffset(0)] internal long                int64;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        struct fieldObjects
        {
            [FieldOffset(0)] internal object              reference;
            [FieldOffset(0)] internal Break               breakIt;
            [FieldOffset(0)] internal IEnumerator         enumerator;
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
        
        public static explicit operator int(TaskContract? contract)
        {
            DBC.Tasks.Check.Require(contract.HasValue, "invalid state");
            DBC.Tasks.Check.Require(contract.Value._currentState == states.value, "invalid state");
            
            return contract.Value._returnValue.int32;
        }
        
        public static explicit operator Break(TaskContract? contract)
        {
            return contract.HasValue && contract.Value._currentState == states.breakit ? contract.Value._returnObjects.breakIt : null;
        }

        public IEnumerator enumerator
        {
            get { return _currentState == states.enumerator ? _returnObjects.enumerator : null; }
        }

        public object reference
        {
            get { return _currentState == states.value ? _returnObjects.reference : null; }
        }

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