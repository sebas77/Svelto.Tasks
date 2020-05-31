#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks
{
    namespace Lean.Unity
    {
        public class EarlyUpdateMonoRunner : EarlyUpdateMonoRunner<IEnumerator<TaskContract>>
        {
            public EarlyUpdateMonoRunner(string name) : base(name)
            {
            }

            public EarlyUpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }

        public class EarlyUpdateMonoRunner<T> : Svelto.Tasks.Unity.EarlyUpdateMonoRunner<LeanSveltoTask<T>>
            where T : IEnumerator<TaskContract>
        {
            public EarlyUpdateMonoRunner(string name) : base(name)
            {
            }

            public EarlyUpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }
    }

    namespace ExtraLean.Unity
    {
        public class EarlyUpdateMonoRunner : EarlyUpdateMonoRunner<IEnumerator>
        {
            public EarlyUpdateMonoRunner(string name) : base(name)
            {
            }

            public EarlyUpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }

        public class EarlyUpdateMonoRunner<T> : Svelto.Tasks.Unity.EarlyUpdateMonoRunner<ExtraLeanSveltoTask<T>>
            where T : IEnumerator
        {
            public EarlyUpdateMonoRunner(string name) : base(name)
            {
            }

            public EarlyUpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }
    }

    namespace Unity
    {
        public class EarlyUpdateMonoRunner<T> : EarlyUpdateMonoRunner<T, StandardRunningInfo> where T : ISveltoTask
        {
            public EarlyUpdateMonoRunner(string name) : base(name, 0, new StandardRunningInfo())
            {
            }

            public EarlyUpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder,
                new StandardRunningInfo())
            {
            }
        }

        public class EarlyUpdateMonoRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
            where TFlowModifier : IFlowModifier
        {
            public EarlyUpdateMonoRunner(string name, uint runningOrder, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                var _processEnumerator = InitializeRunner(modifier);

                UnityCoroutineRunner.StartEarlyUpdateCoroutine(_processEnumerator, runningOrder);
            }
        }
    }
}
#endif