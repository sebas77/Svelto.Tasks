using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks.Enumerators
{
    public abstract class WaitForState<T, W> where T : WaitForState<T, W> where W : Enum
    {
        protected WaitForState(W startingState)
        {
            _state = startingState;
        }

        public WaitForEnumerator Generate()
        {
            return new WaitForEnumerator(_state, this);
        }

        void SignalStateChange(W newState)
        {
            _state = newState;
        }

        public class WaitForEnumerator : IEnumerator
        {
            public object Current => throw new NotSupportedException();

            public WaitForEnumerator(W startingState, WaitForState<T, W> state)
            {
                _stateToWaitFor = startingState;
                _state          = state;
            }

            public IEnumerator WaitFor(W state)
            {
                _startToWait    = true;
                _stateToWaitFor = state;

                return this;
            }

            public bool MoveNext()
            {
                if (_startToWait == false) return true;

                var moveNext = !(EqualityComparer<W>.Default.Equals(_stateToWaitFor, _state._state));

                if (moveNext == false)
                    _startToWait = false;

                return moveNext;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            W                           _stateToWaitFor;
            readonly WaitForState<T, W> _state;
            bool                        _startToWait;

            public void SignalStateChange(W state)
            {
                _state.SignalStateChange(state);
            }
        }

        readonly WaitForEnumerator _wait;

        W _state;
    }
}