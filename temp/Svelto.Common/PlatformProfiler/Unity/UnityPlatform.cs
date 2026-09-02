#if UNITY_2018_3_OR_NEWER && ENABLE_PLATFORM_PROFILER && DEBUG
using System;
using Svelto.Common.Internal;
using Unity.Profiling;

namespace Svelto.Common
{
    public ref struct DisposableSampler
    {
        ProfilerMarker _marker;
        
        public DisposableSampler(ProfilerMarker marker)
        {
            _marker = marker;
            _marker.Begin();
        }

        public void Dispose()
        {
            _marker.End();
        }

        public PauseProfiler Yield() { return new PauseProfiler(_marker); }
    }

    public struct PlatformProfilerMT: IPlatformProfiler
    {
        public PlatformProfilerMT(string info)
        {
            _platformProfilerImplementation = new PlatformProfiler(info);
        }

        public DisposableSampler Sample(string samplerName)
        {
            return _platformProfilerImplementation.Sample(samplerName);
        }

        public DisposableSampler Sample<W>(W sampled)
        {
            return _platformProfilerImplementation.Sample(sampled);
        }

        PlatformProfiler _platformProfilerImplementation;
    }

    public struct PlatformProfiler: IPlatformProfiler
    {
        ProfilerMarker? _marker;

        public PlatformProfiler(string info)
        {
            _marker = new ProfilerMarker(info);
        }

        public static PlatformProfiler PreCreate(string info)
        {
            return new PlatformProfiler()
            {
                _marker = new ProfilerMarker(info)
            };
        }

        public DisposableSampler Sample()
        {
            return new DisposableSampler(this._marker.Value);
        }

        public DisposableSampler Sample(string samplerName)
        {
            return new DisposableSampler(new ProfilerMarker(samplerName));
        }

        public DisposableSampler Sample<T>(T sampled)
        {
            return Sample(sampled.TypeName());
        }
    }

    public readonly struct PauseProfiler: IDisposable
    {
        public PauseProfiler(ProfilerMarker maker)
        {
            _maker = maker;
            _maker.End();
        }

        public void Dispose()
        {
            _maker.Begin();
        }

        readonly ProfilerMarker _maker;
    }
}
#endif