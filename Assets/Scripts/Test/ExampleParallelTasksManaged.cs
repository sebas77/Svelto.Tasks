using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Experimental;
using UnityEngine;

class SomeData
{
    public int justForTest;
}

class LoadSomething : IEnumerable, IChainLink<SomeData>
{
    public LoadSomething(WWW wWW)
    {
        this.wWW = wWW;
    }

    #region IChainLink implementation

    public SomeData token { set; private get; }

    #endregion

    public IEnumerator GetEnumerator()
    {
        yield return new WWWEnumerator(wWW);

        foreach (var s in wWW.responseHeaders.Values)
            Debug.Log(s);

        token.justForTest++;
    }

    WWW wWW;
}

public class ExampleParallelTasksManaged : MonoBehaviour 
{
    [TextArea]
    public string Notes = "This example shows how to run different types of tasks in Parallel with the TaskRunner (using time-spliting technique)";
    // Use this for initialization
    void Start () 
    {
        var someData = new SomeData();

        var pt = new ParallelTaskCollection<SomeData>(someData);
        var st = new SerialTaskCollection<SomeData>(someData);
        
        st.Add(Print("s1"));
        st.Add(Print("s2"));
        st.Add(pt);
        st.Add(Print("s3"));
        st.Add(Print("s4"));
        
        pt.Add(Print("1"));
        pt.Add(Print("2"));
        pt.Add(DoSomethingAsynchonously());
        pt.Add(new LoadSomething(new WWW("www.google.com"))); //obviously the token could be passed by constructor, but in some complicated situations, this is not possible (usually while exploiting continuation)
        pt.Add(new LoadSomething(new WWW("http://download.thinkbroadband.com/5MB.zip")));
        pt.Add(new LoadSomething(new WWW("www.ebay.com")));
        pt.Add(Print("3"));
        pt.Add(Print("4"));
        pt.Add(Print("5"));
        pt.Add(Print("6"));
        pt.Add(Print("7"));
        pt.Add(Print(someData.justForTest.ToString()));
            
        TaskRunner.Instance.Run(st);
    }

    void Update()
    {
        if (Input.anyKeyDown)
            if (_paused == false)
            {
                Debug.LogWarning("Paused!");
                TaskRunner.Instance.PauseAllTasks();
                _paused = true;
            }
            else
            {
                Debug.LogWarning("Resumed!");
                _paused = false;
                TaskRunner.Instance.ResumeAllTasks();
            }
    }

    IEnumerator DoSomethingAsynchonously() //this can be awfully slow, since it is synched with the framerate
    {
        //try this in place of the next one to see how default unity yields (all of them including www) will break the parallelism      
        //yield return new WaitForSeconds(5);
        yield return new WaitForSecondsEnumerator(5);
    }

    IEnumerator Print(string i)
    {
        Debug.Log(i);

        yield return null;
    }

    bool _paused;
}
