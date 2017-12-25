using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Test.Editor;
using UnityEngine;

namespace Test.MultiThread
{
    public class ExampleParallelTasksManagedMT : MonoBehaviour 
    {
        [TextArea]
        public string Notes = "This example shows how to run a collection of tasks on another thread. Some tasks exploit continuation to wait the main thread execution.";
        // Use this for initialization

        void OnEnable()
        {
            UnityConsole.Clear();
        }

        void Start () 
        {
            ParallelTaskCollection pt = new ParallelTaskCollection();
            SerialTaskCollection   st = new SerialTaskCollection();
            
            st.Add(Print("s1"));
            st.Add(Print("s2"));
            st.Add(Print("s3"));
            st.Add(Print("s4"));
            
            pt.Add(Print("p1")).Add(Print("p2"));
            pt.Add(new LoadSomething(new WWWEnumerator(new WWW("www.google.com"))).GetEnumerator()); //obviously the token could be passed by constructor, but in some complicated situations, this is not possible (usually while exploiting continuation)
            pt.Add(new LoadSomething(new WWWEnumerator(new WWW("http://download.thinkbroadband.com/5MB.zip"))).GetEnumerator());
            pt.Add(new LoadSomething(new WWWEnumerator(new WWW("www.ebay.com"))).GetEnumerator());

            pt.Add(Print("p3")).Add(Print("p4")).Add(st).Add(Print("p5")).Add(Print("p6")).Add(Print("p7"));

            TaskRunner.Instance.RunOnSchedule(MTRunner, pt); //running on another thread!
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

        void OnApplicationQuit()
        {
            MTRunner.StopAllCoroutines(); //Unity will get stuck for ever if you don't do this
        }

        IEnumerator Print(string i)
        {
            Debug.Log(i);
            
            yield return null;
        }

        bool _paused;
        MultiThreadRunner MTRunner = new MultiThreadRunner("ExampleParallelTasksManagedMT");
    }

    class LoadSomething : IEnumerable
    {
        public SomeData token { set; private get; }

        public LoadSomething(WWWEnumerator wWW)
        {
            this.wWW = wWW;
            task = TaskRunner.Instance.AllocateNewTaskRoutine();
            task.SetEnumeratorProvider(DoIt);
        }

        public IEnumerator GetEnumerator()
        {
            yield return task.ThreadSafeStart(); //Continuation! The task will continue on the main thread scheduler!
        }

        IEnumerator DoIt()
        {
            yield return wWW;

            foreach (string s in wWW.www.responseHeaders.Values)
                Debug.Log(s);
        }

        WWWEnumerator   wWW;
        ITaskRoutine    task;
    }
}
