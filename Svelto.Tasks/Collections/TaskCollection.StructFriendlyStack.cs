using System;
using System.Collections;

namespace Svelto.Tasks
{
    public abstract partial class TaskCollection<T>
    {
        protected struct StructFriendlyStack
        {
            IEnumerator[] _stack;
            int _nextFreeStackIndex;

            public bool isValid() { return _stack != null; }
            public int count => _nextFreeStackIndex;

            public StructFriendlyStack(int stackSize)
            {
                _stack              = new IEnumerator[stackSize];
                _nextFreeStackIndex = 0;
            }

            public void Push(in IEnumerator value)
            {
                // Don't reallocate before we actually want to push to it
                if (_nextFreeStackIndex == _stack.Length)
                {
                    // Double for small stacks, and increase by 20% for larger stacks
                    Array.Resize(ref _stack, _stack.Length < 100 ? 2 *_stack.Length : (int) (_stack.Length * 1.2));
                }

                // Store the value, and increase reference afterwards
                _stack[_nextFreeStackIndex++] = value;
            }

            public T Pop()
            {
                if(_nextFreeStackIndex == 0)
                    throw new InvalidOperationException("The stack is empty");

                // Decrease the reference before fetching the value as
                // the reference points to the next free place
                IEnumerator returnValue = _stack[--_nextFreeStackIndex]; 

                // As a safety/security measure, reset value to a default value
                _stack[_nextFreeStackIndex] = default(T);

                return returnValue as T;
            }

            public ref IEnumerator Peek()
            {
                DBC.Tasks.Check.Require(_nextFreeStackIndex != 0);
                
                return ref _stack[_nextFreeStackIndex - 1];
            }

            public void Clear()
            {
                Array.Clear(_stack, 0, _stack.Length);
                
                _nextFreeStackIndex = 0;
            }
        }
    }
}