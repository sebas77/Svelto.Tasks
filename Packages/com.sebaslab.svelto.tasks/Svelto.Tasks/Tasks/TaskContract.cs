using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks
{
    public readonly struct TaskContract
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
        
        public TaskContract(Exception o) : this()
        {
            _currentState            = States.exception;
            _returnObjects.reference = o;
        }
        
        internal TaskContract(Continuation continuation, bool continueCase = false) : this()
        {
            _currentState = continueCase ? States.sameRunnerContinuation : States.continuation;
            _continuation = continuation;
        }

        internal TaskContract(IEnumerator<TaskContract> enumerator, bool fireAndForget = false) : this()
        {
            DBC.Tasks.Check.Require(enumerator != null);
            _currentState = fireAndForget       ? States.forgetLeanEnumerator : States.leanEnumerator;
            _returnObjects.reference = enumerator;
        }
        
        internal TaskContract(IEnumerator enumerator) : this()
        {
            DBC.Tasks.Check.Require(enumerator != null);
            _currentState = States.extraLeanEnumerator;
            
            _returnObjects.reference = enumerator;
        }

        TaskContract(Break breakit) : this()
        {
            _currentState          = States.breakit;
            _returnObjects.breakMode = breakit;
        }

        TaskContract(Yield o) : this()
        {
            _currentState            = States.yieldit;
        }
        
        TaskContract(Continue o) : this()
        {
            _currentState            = States.continueIt;
        }
        
        TaskContract(object reference, bool isReference): this() //I am using this convoluted way because I do not want the compiler to get confused and use an object constructor by mistake
        {
            _currentState = States.reference;
            _returnObjects.reference = reference;
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
        
        public static implicit operator TaskContract(Continue continueIt)
        {
            return new TaskContract(continueIt);
        }

        public static implicit operator TaskContract(string payload)
        {
            return new TaskContract(payload);
        }
        
        public static TaskContract FromReference(object reference)
        {
            return new TaskContract (reference, true);
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

        internal Break breakMode => _currentState == States.breakit ? _returnObjects.breakMode : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool isTaskEnumerator(out (IEnumerator<TaskContract> enumerator, bool isFireAndForget) tuple)
        {
            if (_currentState == States.leanEnumerator)
            {
                tuple.enumerator = (IEnumerator<TaskContract>)_returnObjects.reference;
                tuple.isFireAndForget = false;
  
                return true;
            }

            if (_currentState == States.forgetLeanEnumerator)
            {
                tuple.enumerator = (IEnumerator<TaskContract>)_returnObjects.reference;
                tuple.isFireAndForget = true;

                return true;
            }

            tuple.enumerator = null;
            tuple.isFireAndForget = false;

            return false;
        }

        internal Continuation? continuation
        {
            get
            {
                if (_currentState != States.continuation && _currentState != States.sameRunnerContinuation)
                    return null;

                return _continuation;
            }
        }

        internal bool hasValue    => _currentState == States.value || _currentState == States.reference || _currentState == States.exception;
        
        internal bool yieldIt     => _currentState == States.yieldit;
        public bool continueIt => _currentState == States.continueIt; //this is meaninful only if the task returns a TaskContract without being an Iterator block

        //if isContinued == true if Continue() task has been yielded
        //if isContinued == false if RunOn() task has been yielded 
        internal bool isContinued => _currentState == States.sameRunnerContinuation;
        
        readonly FieldValues  _returnValue;
        readonly FieldObjects _returnObjects;
        readonly States       _currentState;
        readonly Continuation _continuation;

        public static IEnumerator<TaskContract> Empty { get; } = EmptyEnumerator();
        
        static IEnumerator<TaskContract> EmptyEnumerator()
        {
            yield break;
        }

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
            [FieldOffset(0)] internal Break  breakMode;
        }

        enum States
        {
            yieldit = 0,
            value,
            continuation,
            breakit,
            leanEnumerator,
            extraLeanEnumerator,
            reference,
            exception,
            forgetLeanEnumerator,
            sameRunnerContinuation,
            continueIt, //immediate MoveNext without yielding
        }
        
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Yield
        {
            public static readonly Yield It = null;
        }
        
        public class Continue
        {
            public static readonly Continue It = new Continue();
        }
        
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Break
        {
            /// <summary>
            /// A Break.It task breaks but to not break the caller task. A task with break.it can be cached if it runs through a while (true) loop.
            /// the task is completed and removed from the queue on each Break.It but the enumerator can be reused from
            /// the calling task next frame as it's not completed for the CLR. This allows to reuse Iterator Blocks instead
            /// to allocate new ones each time.
            ///
            /// A task can return yield return break, but yield return break will end the state machine life, while Break.It will keep it alive
            /// </summary>
            public static readonly Break It = new Break();
            /// <summary>
            /// Break.AndStop breaks the task and the caller tasks too (propagates the break to the caller task).
            /// </summary>
            public static readonly Break AndStop = new Break();
            
            //TODO URGENT: IS THERE ANY DIFFERENCE ANYMORE BETWEEN BREAK.IT AND BREAK.ANDSTOP? 
            public bool AnyBreak => this == It || this == AndStop;
        }
    }
    
    //leaving this in case I came up with this smart idea again: THIS CANNOT BE DONE!
    //UNFORTUNATELY IENUMERATOR MUST BE USED EXPLICITLY TO BE RECOGNISED AS ITERATOR BLOCK
//    public interface ITaskEnumerator: IEnumerator<TaskContract>
//    {
//    }
}