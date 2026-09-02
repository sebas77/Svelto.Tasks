using System;
using Svelto.DataStructures;

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
        void AddTask(in T task, (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index);
    }
}