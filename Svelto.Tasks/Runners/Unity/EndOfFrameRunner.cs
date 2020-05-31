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
        public class EndOfFrameMonoRunner:EndOfFrameMonoRunner<IEnumerator<TaskContract>>
        {
            public EndOfFrameMonoRunner(string name) : base(name)
            {
            }
            public EndOfFrameMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }

        public class EndOfFrameMonoRunner<T> : Svelto.Tasks.Unity.EndOfFrameMonoRunner<LeanSveltoTask<T>> where T : IEnumerator<TaskContract>
        {
            public EndOfFrameMonoRunner(string name) : base(name)
            {
            }
            public EndOfFrameMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }
    }

    namespace ExtraLean.Unity
    {
        public class EndOfFrameMonoRunner:EndOfFrameMonoRunner<IEnumerator>
        {
            public EndOfFrameMonoRunner(string name) : base(name)
            {
            }
            public EndOfFrameMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }

        public class EndOfFrameMonoRunner<T> : Svelto.Tasks.Unity.EndOfFrameMonoRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
        {
            public EndOfFrameMonoRunner(string name) : base(name)
            {
            }
            public EndOfFrameMonoRunner(string name, uint runningOrder) : base(name, runningOrder)
            {
            }
        }
    }

    namespace Unity
    {
        public class EndOfFrameMonoRunner<T> : EndOfFrameMonoRunner<T, StandardRunningInfo> where T : ISveltoTask
        {
            public EndOfFrameMonoRunner(string name) : base(name, 0, new StandardRunningInfo())
            {
            }
            public EndOfFrameMonoRunner(string name, uint runningOrder) : base(name, runningOrder, new StandardRunningInfo())
            {
            }
        }

        public class EndOfFrameMonoRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
                                                                        where TFlowModifier : IFlowModifier
        {
            public EndOfFrameMonoRunner(string name, uint runningOrder, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                var _processEnumerator = InitializeRunner(modifier);

                UnityCoroutineRunner.StartEndOfFrameCoroutine(_processEnumerator, runningOrder);
            }
        }
    }
}
#endif
