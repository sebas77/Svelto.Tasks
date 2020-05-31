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
        public class UpdateMonoRunner : UpdateMonoRunner<IEnumerator<TaskContract>>
        {
            public UpdateMonoRunner(string name) : base(name) {}
            public UpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }

        public class UpdateMonoRunner<T> : Svelto.Tasks.Unity.UpdateMonoRunner<LeanSveltoTask<T>>
            where T : IEnumerator<TaskContract>
        {
            public UpdateMonoRunner(string name) : base(name) {}
            public UpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }
    }

    namespace ExtraLean.Unity
    {
        public class UpdateMonoRunner : UpdateMonoRunner<IEnumerator>
        {
            public UpdateMonoRunner(string name) : base(name) {}
            public UpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }

        public class UpdateMonoRunner<T> : Svelto.Tasks.Unity.UpdateMonoRunner<ExtraLeanSveltoTask<T>> where T : IEnumerator
        {
            public UpdateMonoRunner(string name) : base(name) {}
            public UpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder) {}
        }
    }

    namespace Unity
    {
        public abstract class UpdateMonoRunner<T> : UpdateMonoRunner<T, StandardRunningInfo> where T : ISveltoTask
        {
            protected UpdateMonoRunner(string name) : base(name, 0, new StandardRunningInfo()) {}
            protected UpdateMonoRunner(string name, uint runningOrder) : base(name, runningOrder,
                new StandardRunningInfo()) {}
        }

        public abstract class UpdateMonoRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
            where TFlowModifier : IFlowModifier
        {
            protected UpdateMonoRunner(string name, uint runningOrder, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                var processEnumerator = InitializeRunner(modifier);

                UnityCoroutineRunner.StartUpdateCoroutine(processEnumerator, runningOrder);
            }
        }
    }
}
#endif