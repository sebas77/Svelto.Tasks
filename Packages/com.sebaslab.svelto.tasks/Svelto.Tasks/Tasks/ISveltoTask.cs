using System;
using Svelto.DataStructures;

namespace Svelto.Tasks
{
    //ISveltoTask is not an enumerator just to avoid ambiguity and understand responsibilities in the other classes
    public interface ISveltoTask
    {
        internal StepState Step(int runningTaskIndexFromRunningTasksToReplace, TombstoneHandle currentSpawnedTaskToRunIndex);
        internal void Stop();
        internal void Dispose();

        bool isCompleted { get; }
        string name { get; }
    }
    
    [Flags]
    public enum StepState
    {
        Invalid = 0,
        Running = 1 << 0,
        Completed = 1 << 1,
        Faulted = 1 << 2,
    }
}

