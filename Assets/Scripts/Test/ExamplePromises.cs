using System;
using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;

namespace Test.Editor
{
    public class ExamplePromises : MonoBehaviour
    {
        [TextArea]
        public string Notes = "This example shows how to use the promises-like features." + 
                              " Pay attention to how Stop a promise, catch a failure and set a race condition ";

        class ValueObject<T>
        {
            public ValueObject(T par)
            {
                target = par;
            }

            public object target;
        }

        void OnEnable()
        {
            UnityConsole.Clear();
        }
        // Use this for initialization
        void Start()
        {
            TaskRunner.Instance.AllocateNewTaskRoutine().
                       SetEnumerator(RunTasks(0.1f)).Start(onStop: OnStop);
        }

        void OnStop()
        {
            Debug.LogWarning("oh oh, did't happen on time, let's try again");

            TaskRunner.Instance.AllocateNewTaskRoutine().
                       SetEnumerator(RunTasks(1000)).Start(OnFail);
        }

        void OnFail(PausableTaskException obj)
        {
            Debug.LogError("tsk tsk");
        }

        IEnumerator RunTasks(float timeout)
        {
            var enumerator = GetURLAsynchronously();

            //wait for one second (simulating async load) 
            yield return enumerator;
        
            string url = (enumerator.Current as ValueObject<string>).target as string;

            var parallelTasks = new ParallelTaskCollection();

            //parallel tasks with race condition (timeout Breaks it)
            parallelTasks.Add(BreakOnTimeOut(timeout));
            parallelTasks.Add(new LoadSomething(new WWW(url)).GetEnumerator());

            yield return parallelTasks;

            if (parallelTasks.Current == Break.It)
            {
                yield return Break.AndStop;

                throw new Exception("should never get here");
            }

            yield return new WaitForSecondsEnumerator(2);
        }

        IEnumerator GetURLAsynchronously()
        {
            yield return new WaitForSecondsEnumerator(1); //well not real reason to wait, let's assume we were running a web service

            yield return new ValueObject<string>("http://download.thinkbroadband.com/5MB.zip");
        }

        IEnumerator BreakOnTimeOut(float timeout) 
        {
            var time = DateTime.Now;
            yield return new WaitForSecondsEnumerator(timeout);
            Debug.Log("time passed: " + (DateTime.Now - time).TotalMilliseconds);

            yield return Break.It;

            //this is the inverse of the standard Promises race function, 
            //achieve the same result as it stops the parallel enumerator 
            //once hit
        }

        class LoadSomething : IEnumerable
        {
            public LoadSomething(WWW wWW)
            {
                this.wWW = wWW;
            }

            public IEnumerator GetEnumerator()
            {
                Debug.Log("download started");

                yield return new ParallelTaskCollection(new [] { new WWWEnumerator(wWW), PrintProgress(wWW) });

                foreach (string s in wWW.responseHeaders.Values)
                    Debug.Log(s);

                Debug.Log("Success! Let's throw an Exception to be caught by OnFail");

                throw new Exception("Dayyym");
            }

            IEnumerator PrintProgress(WWW wWW)
            {
                while (wWW.isDone == false)
                {
                    Debug.Log(wWW.progress);

                    yield return null;
                }
            }

            WWW wWW;
        }
    }
}
