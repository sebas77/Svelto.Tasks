using System;

namespace Svelto.Tasks
{
    public abstract partial class TaskCollection<T>
    {
        protected struct StructFriendlyStack
        {
            T[] _stack;
            int _nextFreeStackIndex;

            public bool isValid() { return _stack != null; }
            public int count => _nextFreeStackIndex;

            public StructFriendlyStack(int stackSize)
            {
                _stack              = new T[stackSize];
                _nextFreeStackIndex = 0;
            }

            public void Push(in T value)
            {
                // Don't reallocate before we actually want to push to it
                if (_nextFreeStackIndex == _stack.Length)
                {
                    // Double for small stacks, and increase by 20% for larger stacks
                    int oldLen   = _stack.Length;
                    int newLen   = oldLen < 100 ? oldLen * 2
                            : oldLen + Math.Max(1, oldLen / 5); // +20 %

                    Array.Resize(ref _stack, newLen);
                }

                // Store the value, and increase reference afterwards
                _stack[_nextFreeStackIndex++] = value;
            }

            public T Pop()
            {
                if (_nextFreeStackIndex == 0)
                    throw new InvalidOperationException("The stack is empty");

                // Step back first (index now points to the last valid element)
                var idx = --_nextFreeStackIndex;
                T value = _stack[idx];

                // Dispose if needed
                value.Dispose();

                _stack[idx] = default;     // safety / GC friendliness
                return value;
            }

            public ref T Peek()
            {
                DBC.Tasks.Check.Require(_nextFreeStackIndex != 0);
                
                return ref _stack[_nextFreeStackIndex - 1];
            }

            public void Clear()
            {
                for (int i = 0; i < _nextFreeStackIndex; i++)
                    _stack[i].Dispose();             // dispose every live enumerator
                
                Array.Clear(_stack, 0, _stack.Length);
                
                _nextFreeStackIndex = 0;
            }
        }
    }
}