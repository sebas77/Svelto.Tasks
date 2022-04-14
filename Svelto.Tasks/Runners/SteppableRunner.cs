using System;
using System.Collections;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    namespace Lean
    {
        public class SteppableRunner : SteppableRunner<LeanSveltoTask<IEnumerator<TaskContract>>>,
            IEnumerator<TaskContract>
        {
            public SteppableRunner(string name) : base(name)
            {
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public TaskContract Current => Yield.It;

            object IEnumerator.Current => throw new NotImplementedException();
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : SteppableRunner<ExtraLeanSveltoTask<IEnumerator>>, IEnumerator
        {
            public SteppableRunner(string name) : base(name)
            {
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public object Current => Yield.It;
        }
    }
}