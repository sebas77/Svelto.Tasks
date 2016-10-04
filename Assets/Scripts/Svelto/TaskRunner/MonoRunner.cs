using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

//
//it doesn't make any sense to have
//more than one MonoRunner active
//
namespace Svelto.Tasks.Internal
{
    class MonoRunner : IRunner
    {
        public bool paused { set; get; }
        public bool stopped { private set; get; }

        public int numberOfRunningTasks { get { return _coroutines.Count; } }

        public MonoRunner()
        {
            _coroutines = new FasterList<IEnumerator>(NUMBER_OF_INITIAL_COROUTINE);

            if (_go == null)
            {
                _go = new GameObject("TaskRunner");

                _runnerBehaviour = _go.AddComponent<RunnerBehaviour>();
                _runnerBehaviourForUnityCoroutine = _go.AddComponent<RunnerBehaviour>();
                _runnerBehaviour.StartCoroutine(StartCoroutineInternal());

                Object.DontDestroyOnLoad(_go);
            }
            else
            {
                RunnerBehaviour[] behaviours = _go.GetComponents<RunnerBehaviour>();
                _runnerBehaviour = behaviours[0];
                _runnerBehaviourForUnityCoroutine = behaviours[1];
            }
        }

        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsability to stop the tasks if needed
        /// </summary>
        public void StopAllCoroutines() 
        {
            stopped = true; paused = false;

            _runnerBehaviourForUnityCoroutine.StopAllCoroutines();
            
            _newTaskRoutines.Clear();

            //note: _coroutines will be cleaned by the single tasks stopping silently.
            //in this way they will be put back to the pool.
            //let's be sure that the runner had the time to stop and recycle the previous tasks
            _waitForflush = true; 
        }

        public void StartCoroutine(PausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        protected IEnumerator StartCoroutineInternal()
        {
            while (true)
            {
                if (_newTaskRoutines.Count > 0 && _waitForflush == false) //don't start anything while flushing
                    _coroutines.AddRange(_newTaskRoutines.DequeueAll());

                for (int i = 0; i < _coroutines.Count; i++)
                {
                    var enumerator = _coroutines[i];

                    try
                    {
                        if (enumerator.MoveNext() == false)
                            _coroutines.UnorderredRemoveAt(i--);
                        else
                        {
                            //let's spend few words about this. Special YieldInstruction can be only processed internally
                            //by Unity. The simplest way to handle them is to hand them to Unity itself. 
                            //However while the Unity routine is processed, the rest of the coroutine is waiting for it.
                            //This would defeat the purpose of the parallel procedures. For this reason, the Parallel
                            //routines will mark the enumerator returned as ParallelYield which will change the way the routine is processed.
                            //in this case the MonoRunner won't wait for the Unity routine to continue processing the next tasks.
                            var current = enumerator.Current;
                            IEnumerator enumeratorToHandle = null;
                            var yield = current as ParallelYield;
                            if (yield != null)
                                current = yield.Current;
                            else
                                enumeratorToHandle = enumerator;

                            if (current is WWW || current is YieldInstruction || current is AsyncOperation)
                            {
                                _runnerBehaviourForUnityCoroutine.StartCoroutine(HandItToUnity(current, enumeratorToHandle));
                                
                                if (enumeratorToHandle != null)
                                    _coroutines.UnorderredRemoveAt(i--);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        string message = "Coroutine Exception: ";

                        Debug.LogException(new CoroutineException(message, e));

                        _coroutines.UnorderredRemoveAt(i--);
                    }
                }

                if (_waitForflush == true && _coroutines.Count == 0)
                {  //this process is more complex than I like, not 100% sure it covers all the cases yet
                    _waitForflush = false;
                    stopped = false;
                }

                yield return null;
            }
        }

        IEnumerator HandItToUnity(object current, IEnumerator enumerator)
        {
            yield return current;
            yield return enumerator;
        }

        FasterList<IEnumerator>      _coroutines;
        ThreadSafeQueue<IEnumerator> _newTaskRoutines = new ThreadSafeQueue<IEnumerator>();
        RunnerBehaviour              _runnerBehaviour;
        RunnerBehaviour              _runnerBehaviourForUnityCoroutine;
        GameObject                   _go;
        bool                         _waitForflush;

        const int                   NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
