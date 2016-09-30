using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleCoroutineEx : MonoBehaviour 
{
    // Use this for initialization
    IEnumerator Start () 
    {
        yield return StartCoroutine(DoSomethingAsynchonouslyOldWay());
        yield return StartCoroutine(DoSomethingAsynchonouslyNewWay());
    }

    IEnumerator DoSomethingAsynchonouslyNewWay()
    {
        var then = DateTime.Now; 
        
        var enumerator = SomethingAsyncHappensNew();
        yield return enumerator;

        Debug.Log(DateTime.Now - then);
        Debug.Log("result: " + enumerator.Current);

        Debug.Log("Unity Coroutine New");
    }

    IEnumerator DoSomethingAsynchonouslyOldWay()
    {
        var then = DateTime.Now;

        var enumerator = SomethingAsyncHappensOld();
        yield return enumerator;
        
        Debug.Log(DateTime.Now - then);
        Debug.Log("result: " + enumerator.Current);

        Debug.Log("Unity Coroutine Old");
    }

    IEnumerator SomethingAsyncHappensNew()
    {
        var enumerator = AnotherTime(0);
        yield return enumerator;
        enumerator = AnotherTime(enumerator.Current);
        yield return enumerator;
        enumerator = AnotherTime(enumerator.Current);
        yield return enumerator;
        yield return enumerator.Current;
    }

    IEnumerator SomethingAsyncHappensOld()
    {
        var enumerator = AnotherTime(0);
        yield return StartCoroutine(enumerator);
        enumerator = AnotherTime(enumerator.Current);
        yield return StartCoroutine(enumerator);
        enumerator = AnotherTime(enumerator.Current);
        yield return StartCoroutine(enumerator);
        yield return enumerator.Current;
    }

    IEnumerator<int> AnotherTime(int start)
    {
        int i;

        for (i = start; i < start + 10; i++)
            yield return 0; //just time slice it, we are not interested on the value
        
        yield return i;
    }
}
