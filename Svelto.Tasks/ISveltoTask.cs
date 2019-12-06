namespace Svelto.Tasks
{
    public interface ISveltoTask
    {
        TaskContract Current { get; }

        bool MoveNext();

        void Stop();

        string name { get; }
    }
}

