using System;
using System.Collections;

//ITaskRoutine allocated explcitly have several features not 
//available on task started implicitly with the extension
//methods.

//TaskRoutine are promises compliant. However the use of Start 
//and Stop can generate different behaviours

//Start();
//Stop();
//Start();

//The new Start will not run immediatly, but will let the Task to stop first
//and trigger the callback;

//Start();
//Start();

//allows to start the task immediatly but the OnStop callback won't be triggered

namespace Svelto.Tasks
{
    public interface ITaskRoutine
    {
        ITaskRoutine SetEnumeratorProvider(Func<IEnumerator> taskGenerator);
        ITaskRoutine SetEnumerator(IEnumerator taskGenerator);
        ITaskRoutine SetScheduler(IRunner runner);

        ContinuationWrapper Start(Action<PausableTaskException> onFail = null, Action onStop = null);
        ContinuationWrapper ThreadSafeStart(Action<PausableTaskException> onFail = null, Action onStop = null);
     
        void Pause();
        void Resume();
        void Stop();
        
        bool isRunning { get; }
    }
}
