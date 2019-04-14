#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Svelto.Common;
using Svelto.Tasks.Internal;
using Svelto.Tasks.Unity.Internal;

namespace Svelto.Tasks
{
    namespace Lean.Unity
    {
        public class PhysicMonoRunner:PhysicMonoRunner<IEnumerator<TaskContract>>
        {
            public PhysicMonoRunner(string name) : base(name)
            {
            }
        }
        
        public class PhysicMonoRunner<T> : Svelto.Tasks.Unity.PhysicMonoRunner<SveltoTask<T>> where T : IEnumerator<TaskContract>
        {
            public PhysicMonoRunner(string name) : base(name)
            {
            }
        }
    }
    
    namespace ExtraLean.Unity
    {
        public class PhysicMonoRunner:PhysicMonoRunner<IEnumerator>
        {
            public PhysicMonoRunner(string name) : base(name)
            {
            }
        }
        
        public class PhysicMonoRunner<T> : Svelto.Tasks.Unity.PhysicMonoRunner<SveltoTask<T>> where T : IEnumerator
        {
            public PhysicMonoRunner(string name) : base(name)
            {
            }
        }
    }

    namespace Unity
    {
        public class PhysicMonoRunner<T> : PhysicMonoRunner<T, StandardRunningTasksInfo> where T : ISveltoTask
        {
            public PhysicMonoRunner(string name) : base(name, new StandardRunningTasksInfo())
            {
            }
        }

        public class PhysicMonoRunner<T, TFlowModifier> : BaseRunner<T> where T : ISveltoTask
                                                                        where TFlowModifier : IRunningTasksInfo
        {
            public PhysicMonoRunner(string name, TFlowModifier modifier) : base(name)
            {
                modifier.runnerName = name;

                _processEnumerator =
                    new CoroutineRunner<T>.Process<TFlowModifier, PlatformProfiler>
                        (_newTaskRoutines, _coroutines, _flushingOperation, modifier);

                UnityCoroutineRunner.StartPhysicCoroutine(_processEnumerator);
            }
        }
    }
}
#endif
