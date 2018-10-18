using System;
namespace Svelto.Tasks
{
#if UNITY_5_3_OR_NEWER    
    class PlatformProfiler : IDisposable
    {
        readonly UnityEngine.Profiling.CustomSampler sampler;

        public PlatformProfiler(string name)
        {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling(name, name);
        }

        public void Dispose()
        {
            UnityEngine.Profiling.Profiler.EndThreadProfiling();
        }

        public DisposableStruct Sample(string samplerName)
        {
            return new DisposableStruct(UnityEngine.Profiling.CustomSampler.Create(samplerName));
        }

        internal struct DisposableStruct : IDisposable
        {
            readonly UnityEngine.Profiling.CustomSampler _sampler;

            public DisposableStruct(UnityEngine.Profiling.CustomSampler customSampler)
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