using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Experimental;
using UnityEngine;

namespace Test.Editor
{
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

        public SomeData token { set; private get; }

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

        void OnEnable()
        {
            UnityConsole.Clear();
        }

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

            //only the task runner can actually handle parallel tasks
            //that return Unity operations (when unity compatible
            //schedulers are used)
            pt.Add(UnityAsyncOperationsMustNotBreakTheParallelism());
            pt.Add(UnityYieldInstructionsMustNotBreakTheParallelism());

            pt.Add(new LoadSomething(new WWW("www.google.com")).GetEnumerator()); //obviously the token could be passed by constructor, but in some complicated situations, this is not possible (usually while exploiting continuation)
            pt.Add(new LoadSomething(new WWW("http://download.thinkbroadband.com/5MB.zip")).GetEnumerator());
            pt.Add(new LoadSomething(new WWW("www.ebay.com")).GetEnumerator());
            pt.Add(Print("3"));
            pt.Add(Print("4"));
            pt.Add(Print("5"));
            pt.Add(Print("6"));
            pt.Add(Print("7"));
            pt.Add(Print(someData.justForTest.ToString()));
            
            TaskRunner.Instance.Run(st);
        }

        IEnumerator UnityAsyncOperationsMustNotBreakTheParallelism()
        {
            Debug.Log("start async operation");
            var res = Resources.LoadAsync("image.jpg");
            yield return res;
            Debug.Log("end async operation " + res.progress);
        }

        IEnumerator UnityYieldInstructionsMustNotBreakTheParallelism()
        {
            Debug.Log("start yield instruction");
            yield return new WaitForSeconds(2);
            Debug.Log("end yield instruction");
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

        IEnumerator Print(string i)
        {
            Debug.Log(i);

            yield break;
        }

        bool _paused;
    }
}