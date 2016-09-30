using System;
using System.Collections;
using Svelto.Tasks;
using UnityEngine;

public class ExamplePromises : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        TaskRunner.Instance.AllocateNewTaskRoutine().SetEnumerator(RunTasks(2)).Start(onStop: OnStop);
    }

    void OnStop()
    {
        Debug.LogWarning("oh oh, did't happen on time, let's try again");

        TaskRunner.Instance.AllocateNewTaskRoutine().SetEnumerator(RunTasks(1000)).Start(OnFail);
    }

    void OnFail(PausableTaskException obj)
    {
        Debug.LogError("tsk tsk");
    }

    IEnumerator RunTasks(int timeout)
    {
        var enumerator = GetURLAsynchronously();
        yield return enumerator;
        
        string url = enumerator.Current as string;

        yield return new[] { BreakOnTimeOut(timeout), new LoadSomething(new WWW(url)).GetEnumerator() }; //yep it will be converted to a Parallel task
    }

    IEnumerator GetURLAsynchronously()
    {
        yield return new WaitForSecondsEnumerator(1); //well not real reason to wait, let's assume we were running a web service

        yield return "http://download.thinkbroadband.com/50MB.zip";
    }

    IEnumerator BreakOnTimeOut(int timeout) 
    {
        var time = DateTime.Now;
        yield return new WaitForSecondsEnumerator(timeout);
        Debug.Log("time passed: " + (DateTime.Now - time).TotalMilliseconds);

        yield return Break.It; //basically is the inverse of the Race Promises function, achieve the same result
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

            yield return new[] { new WWWEnumerator(wWW), PrintProgress(wWW) };

            foreach (string s in wWW.responseHeaders.Values)
                Debug.Log(s);

            Debug.Log("Success! Let's throw an Exception because I am crazy");

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
