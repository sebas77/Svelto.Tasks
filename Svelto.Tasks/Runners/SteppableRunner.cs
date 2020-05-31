using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks
{
    namespace Lean
    {
        public class SteppableRunner : BaseRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            public SteppableRunner(string name) : base(name)
            {
                var modifier = new StandardRunningInfo {runnerName = name};

                InitializeRunner(modifier);
            }
        }
    }

    namespace ExtraLean
    {
        public class SteppableRunner : BaseRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            public SteppableRunner(string name) : base(name)
            {
                var modifier = new StandardRunningInfo {runnerName = name};

                InitializeRunner(modifier);
            }
        }
    }
}