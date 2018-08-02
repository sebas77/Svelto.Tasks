using System;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Profiling;
#endif
namespace Svelto.Tasks
{
#if UNITY_5_3_OR_NEWER    
    class PlatformProfiler : IDisposable
    {
        readonly CustomSampler sampler;

        public PlatformProfiler(string name)
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling(name, name);
        }

        public void Dispose()
        {
            UnityEngine.Profiling.Profiler.EndSample();
        }

        public DisposableStruct Sample(string samplerName)
        {
            return new DisposableStruct(CustomSampler.Create(samplerName));
        }

        internal struct DisposableStruct : IDisposable
        {
            readonly CustomSampler _sampler;

            public DisposableStruct(CustomSampler customSampler)
            {
                _sampler = customSampler;
                _sampler.Begin();
            }

            public void Dispose()
            {
                _sampler.End();
            }
        }
    }
#else
    class PlatformProfiler : IDisposable
    {
        public PlatformProfiler(string name)
        {}

        public void Dispose()
        {}

        public DisposableStruct Sample()
        {
            return new DisposableStruct();
        }

        internal struct DisposableStruct : IDisposable
        {
            public DisposableStruct()
            {}

            public void Dispose()
            {}
        }
    }
#endif    
}