#if SVELTO_BURST
using Svelto.Tasks.Parallelism;

/// <summary>
/// Splits a concrete Burst range task across the collection's worker threads.
/// The collection performs managed scheduling only. Each concrete task owns the
/// statically-known call to its non-generic Burst entry point.
/// </summary>
public sealed class MultiThreadedBurstParallelTaskCollection<TTask> :
    Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<TTask>
    where TTask : unmanaged, IBurstParallelTask
{
    public MultiThreadedBurstParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks) :
        base(name, numberOfThreads, tightTasks)
    {
    }

    public void Add(in TTask prototype, int iterations)
    {
        if (isRunning == true)
            throw new MultiThreadedParallelTaskCollectionException(
                "can't add tasks on a started MultiThreadedParallelTaskCollection");

        int rangeCount = _runners.Length;
        int iterationsPerRange = iterations / rangeCount;
        int remainder = iterations % rangeCount;

        for (int range = 0; range < rangeCount; range++)
        {
            TTask rangeTask = prototype;
            rangeTask.SetRange(range * iterationsPerRange, iterationsPerRange);
            base.Add(rangeTask);
        }

        if (remainder > 0)
        {
            TTask remainderTask = prototype;
            remainderTask.SetRange(iterationsPerRange * rangeCount, remainder);
            base.Add(remainderTask);
        }
    }
}
#endif
