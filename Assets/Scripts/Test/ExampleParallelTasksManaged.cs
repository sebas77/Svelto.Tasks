using System.Collections;
using Svelto.Tasks;
using UnityEngine;
using Svelto.Tasks.Experimental;

class SomeData
{
    public int justForTest;
}

class LoadSomething : IEnumerable, IChainLink<SomeData>
{
    private WWW wWW;

    #region IChainLink implementation

    public SomeData token { set; private get; }

    #endregion

    public LoadSomething(WWW wWW)
    {
        this.wWW = wWW;
    }

    public IEnumerator GetEnumerator()
    {
        yield return new WWWEnumerator(wWW);

        foreach (string s in wWW.responseHeaders.Values)
            Debug.Log(s);

        token.justForTest++;
    }
}

public class ExampleParallelTasksManaged : MonoBehaviour 
{
    int i;
    
    // Use this for initialization
    void Start () 
    {
        var someData = new SomeData();

        ParallelTaskCollection<SomeData> pt = new ParallelTaskCollection<SomeData>(someData);
        SerialTaskCollection<SomeData>   st = new SerialTaskCollection<SomeData>(someData);
        
        st.Add(Print("s1"));
        st.Add(Print("s2"));
        st.Add(DoSomethingAsynchonously());
        st.Add(Print("s3"));
        st.Add(Print("s4"));
        
        pt.Add(Print("1"));
        pt.Add(Print("2"));
        pt.Add(new LoadSomething(new WWW("www.google.com"))); //obviously the token could be passed by constructor, but in some complicated situations, this is not possible (usually while exploiting continuation)
        pt.Add(new LoadSomething(new WWW("http://download.thinkbroadband.com/5MB.zip")));
        pt.Add(new LoadSomething(new WWW("www.ebay.com")));
        pt.Add(Print("3"));
        pt.Add(Print("4"));
        pt.Add(st);
        pt.Add(Print("5"));
        pt.Add(Print("6"));
        pt.Add(Print("7"));
        pt.Add(Print(someData.justForTest.ToString()));
            
        TaskRunner.Instance.Run(pt);
    }
    
    void Update()
    {
        if (Input.anyKeyDown)
        {
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
    }
    
    IEnumerator DoSomethingAsynchonously() //this can be awfully slow, since it is synched with the framerate
    {
         for (i = 0; i < 50; i++)
            yield return i;
            
        Debug.Log("index " + i);
    }
    
    IEnumerator Print(string i)
    {
        Debug.Log(i);
        yield return null;
    }

    private bool _paused = false;
}
