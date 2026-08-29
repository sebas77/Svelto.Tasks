using Svelto.Tasks.Parallelism;
using Svelto.Tasks.Parallelism.Internal;
/// <summary>
/// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
/// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
/// </summary>
///
public class
        MultiThreadedParallelJobCollection<TJob> :  Svelto.Tasks.Parallelism.ExtraLean.MultiThreadedParallelTaskCollection<ParallelRunEnumerator<TJob>>
        where TJob : struct, ISveltoJob
{
    //works similarly to Unity Jobs, the same job is split into different iterations, the work is then divided according to the indexed iterations
    public void Add(in TJob job, int iterations)
    {
        DBC.Tasks.Check.Require(isRunning == false,
            "can't add tasks on a started MultiThreadedParallelJobCollection");

        var runnersLength   = _runners.Length;
        int tasksPerThread     = (int)System.MathF.Floor((float)iterations / runnersLength);
        int reminder           = iterations % runnersLength;

        for (int i = 0; i < runnersLength; i++)
            Add(new ParallelRunEnumerator<TJob>(job, tasksPerThread * i, tasksPerThread));

        if (reminder > 0)
            Add(new ParallelRunEnumerator<TJob>(job, tasksPerThread * runnersLength, reminder));
    }

    public MultiThreadedParallelJobCollection(string name, uint numberOfThreads, bool tightTasks) : base(name,
        numberOfThreads, tightTasks)
    {
    }
}