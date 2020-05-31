#if UNITY_5 || UNITY_5_3_OR_NEWER
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Unity.Internal;
using System.Collections;
using System.Collections.Generic;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    namespace Lean.Unity
    {
        public class OnGuiRunner : OnGuiRunner<IEnumerator<TaskContract>>
        {
            public OnGuiRunner(string name) : base(name) {}
            public OnGuiRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }

        public class OnGuiRunner<T> : Svelto.Tasks.Unity.OnGuiRunner<LeanSveltoTask<T>>
            where T : IEnumerator<TaskContract>
        {
            public OnGuiRunner(string name) : base(name) {}
            public OnGuiRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }
    }

    namespace ExtraLean.Unity
    {
        public class OnGuiRunner : OnGuiRunner<IEnumerator>
        {
            public OnGuiRunner(string name) : base(name) {}
            public OnGuiRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }

        public class OnGuiRunner<T> : Svelto.Tasks.Unity.OnGuiRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
        {
            public OnGuiRunner(string name) : base(name) {}
            public OnGuiRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }
    }

    namespace Unity
    {
        public abstract class OnGuiRunner<T> : OnGuiRunner<T, StandardRunningInfo> where T : ISveltoTask
        {
            protected OnGuiRunner(string name) : base(name, 0, new StandardRunningInfo()) {}
            protected OnGuiRunner(string name, uint runningOrder) : base(name, runningOrder,
                new StandardRunningInfo()) {}
        }

        public abstract class OnGuiRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
            where TFlowModifier : IFlowModifier
        {
            protected OnGuiRunner(string name, uint runningOrder, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                var _processEnumerator = InitializeRunner(modifier);

                UnityCoroutineRunner.StartOnGuiCoroutine(_processEnumerator, runningOrder);
            }
        }
    }
}
#endif