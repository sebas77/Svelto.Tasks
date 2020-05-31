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
        public class LateMonoRunner:LateMonoRunner<IEnumerator<TaskContract>>
        {
            public LateMonoRunner(string name) : base(name) { }
            public LateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) { }
        }

        public class LateMonoRunner<T> : Svelto.Tasks.Unity.LateMonoRunner<LeanSveltoTask<T>> where T : IEnumerator<TaskContract>
        {
            public LateMonoRunner(string name) : base(name) { }
            public LateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) { }
        }
    }

    namespace ExtraLean.Unity
    {
        public class LateMonoRunner: LateMonoRunner<IEnumerator>
        {
            public LateMonoRunner(string name) : base(name) { }
            public LateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) { }
        }

        public class LateMonoRunner<T> : Svelto.Tasks.Unity.LateMonoRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
        {
            public LateMonoRunner(string name) : base(name) { }
            public LateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) { }
        }
    }

    namespace Unity
    {
        public class LateMonoRunner<T> : LateMonoRunner<T, StandardRunningInfo> where T : ISveltoTask
        {
            public LateMonoRunner(string name) : base(name, 0, new StandardRunningInfo()) { }
            public LateMonoRunner(string name, uint runningOrder) : base(name, runningOrder, new StandardRunningInfo()) { }
        }

        public class LateMonoRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
                                                                        where TFlowModifier : IFlowModifier
        {
            public LateMonoRunner(string name, uint runningOrder, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                var _processEnumerator = InitializeRunner(modifier);

                UnityCoroutineRunner.StartLateCoroutine(_processEnumerator, runningOrder);
            }
        }
    }
}
#endif
