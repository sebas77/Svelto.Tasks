using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.FlowModifiers;

namespace Svelto.Tasks
{
    /// Generic Steppable Runners. They can be used to run tasks that can be stepped manually.
    namespace Lean
    {
        public class SteppableRunner : GenericSteppableRunner<LeanSveltoTask<IEnumerator<TaskContract>>>,
                IEnumerator<TaskContract>, IGenericLeanRunner
        {
            public SteppableRunner(string name) : base(name)
            {
                UseFlowModifier(new StandardFlow());
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public TaskContract Current => TaskContract.Yield.It;

            object IEnumerator.Current => throw new NotImplementedException();
        }
        
        public class SteppableRunner<T> : GenericSteppableRunner<LeanSveltoTask<T>>, IEnumerator<TaskContract>, IGenericLeanRunner<T>
            where T : IEnumerator<TaskContract>
        {
            public SteppableRunner(string name) : base(name)
            {
                UseFlowModifier(new StandardFlow());
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            {
            }

            public TaskContract Current => TaskContract.Yield.It;

            object IEnumerator.Current => throw new NotImplementedException();
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : GenericSteppableRunner<ExtraLeanSveltoTask<IEnumerator>>, IEnumerator,IGenericExtraLeanRunner
        {
            public SteppableRunner(string name) : base(name)
            {
                UseFlowModifier(new StandardFlow());
            }

            public bool MoveNext()
            {
                return Step();
            }

            public void Reset()
            { }

            public object Current => TaskContract.Yield.It;
        }
    }
}