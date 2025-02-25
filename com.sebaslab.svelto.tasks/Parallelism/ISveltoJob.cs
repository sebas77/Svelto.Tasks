namespace Svelto.Tasks.Parallelism
{
    public interface ISveltoJob
    {
        void Update(int jobIndex);
    }
}