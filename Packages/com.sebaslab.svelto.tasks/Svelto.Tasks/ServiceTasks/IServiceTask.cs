using System;
using System.Collections.Generic;

namespace Svelto.Tasks
{
    //todo a task that can report progress should implement this interface
#if TO_IMPLEMENT_PROPERLY
    public interface ITaskProgress
    {
        float		progress { get; }
    }
#endif
    public interface IServiceTask
    {
        bool isDone { get; }
        
        IEnumerator<TaskContract> Execute();	
    }

    public interface IServiceTaskExceptionHandler
    {
        Exception   throwException { get; }
    }
}

