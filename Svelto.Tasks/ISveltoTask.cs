using System.Collections.Generic;

namespace Svelto.Tasks
{
    //ISveltoTask is not an enumerator just to avoid ambiguity and understand responsibilities in the other classes
    public interface ISveltoTask
    {
        bool MoveNext();

        void Stop();
        
        bool isCompleted { get; }

        string name { get; }
    }
}

