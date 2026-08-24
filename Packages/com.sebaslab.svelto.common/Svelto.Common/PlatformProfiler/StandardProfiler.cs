using System;
using System.Diagnostics;
using System.Threading;

namespace Svelto.Common
{
    public struct StandardProfiler
    {
        static readonly ThreadLocal<Stopwatch> _stopwatch = new ThreadLocal<Stopwatch>(() =>
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        });

        readonly string _info;
        
        //It doesn't make any sense to profile with two different patterns, either it's through the main struct
        //or through the Sample method. If both are provided, Sample is basically never used.
        public StandardProfiler(string info)
        {
            _info = info;
        }

        public StandardDisposableSamplerHolder Sample()
        {
            return new StandardDisposableSamplerHolder(_stopwatch.Value);
        }

        public StandardDisposableSampler SampleAndLog(string samplerName)
        {
            return new StandardDisposableSampler(_info, samplerName, _stopwatch.Value);
        }
    }
    
    public struct StandardDisposableSampler: IDisposable
    {
        readonly string    _profilerName;
        readonly Stopwatch _watch;
        readonly long      _startTime;
        readonly string    _samplerName;
        uint               _elapsed;

        public StandardDisposableSampler( string profilerName, string samplerName, Stopwatch stopwatch)
        {
            _profilerName = profilerName;
            _watch       = stopwatch;
            _startTime   = stopwatch.ElapsedMilliseconds;
            _samplerName = samplerName;
            _elapsed     = 0;
        }

        public void Dispose()
        {
            _elapsed = (uint)(_watch.ElapsedMilliseconds - _startTime);

            Console.Log(_profilerName.FastConcat(" -> ", _samplerName).FastConcat(" -> ").FastConcat(_elapsed).FastConcat(" ms"));
        }
    }

    public struct StandardDisposableSamplerHolder:IDisposable
    {
        static readonly double _ticksToNano = 1_000_000_000.0 / Stopwatch.Frequency;

        readonly Stopwatch _watch;
        readonly long      _startTime;
        readonly long      _startTicks;
        uint               _elapsed;
        long               _elapsedNano;
        bool               _isDisposed;

        public uint ElapsedMs => _isDisposed ? _elapsed : (uint)(_watch.ElapsedMilliseconds - _startTime);
        public long ElapsedNano => _isDisposed ? _elapsedNano : (long)((_watch.ElapsedTicks - _startTicks) * _ticksToNano);

        public StandardDisposableSamplerHolder(  Stopwatch stopwatch)
        {
            _watch        = stopwatch;
            _startTime    = stopwatch.ElapsedMilliseconds;
            _startTicks   = stopwatch.ElapsedTicks;
            _elapsed      = 0;
            _elapsedNano  = 0;
            _isDisposed   = false;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _elapsed = (uint)(_watch.ElapsedMilliseconds - _startTime);
            _elapsedNano = (long)((_watch.ElapsedTicks - _startTicks) * _ticksToNano);
        }
    }
}