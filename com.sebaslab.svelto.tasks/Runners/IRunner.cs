using System;
using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks
{
    public interface IRunner : IDisposable
    {
    }

    public interface ISteppableRunner : IRunner
    {
        bool Step();
        bool hasTasks { get; }
    }

    public interface IRunner<T> : IRunner where T : ISveltoTask
    {
        void AddTask( in T task, (int runningTaskIndexToReplace, int parentSpawnedTaskIndex) index);
    }
}