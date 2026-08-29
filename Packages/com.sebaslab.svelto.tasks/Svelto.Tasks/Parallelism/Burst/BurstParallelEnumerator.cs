namespace Svelto.Tasks.Parallelism
{
    /// <summary>
    /// A concrete range task whose MoveNext implementation calls a non-generic
    /// Burst direct-call entry point. SetRange runs only while the collection is
    /// being configured; it is not part of the Burst execution boundary.
    /// </summary>
    public interface IBurstParallelTask : IParallelTask
    {
        void SetRange(int startIndex, int count);
    }
}

