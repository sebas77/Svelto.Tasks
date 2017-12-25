using System.Collections;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using UnityEngine;

namespace Test.Editor
{
    public class ExampleParallelTasks : MonoBehaviour 
    {
        int i;

        [TextArea]
        public string Notes = "This example shows how to run different types of tasks in Parallel (using time-spliting technique)";

        void OnEnable()
        {
            UnityConsole.Clear();
        }

        // Use this for initialization
        void Start () 
        {
            Application.targetFrameRate = 60;

            Debug.Log("Set frame rate to 60fps");
		
            ParallelTaskCollection pt = new ParallelTaskCollection();
            SerialTaskCollection	st = new SerialTaskCollection();
		
            st.Add(Print("s1"));
            st.Add(DoSomethingAsynchonously());
            st.Add(Print("s3"));
		
            pt.Add(Print("1"));
            pt.Add(Print("2"));
            pt.Add(Print("3"));
            pt.Add(Print("4"));
            pt.Add(Print("5"));
            pt.Add(st);
            pt.Add(Print("6"));
            pt.Add(WWWTest ());
            pt.Add(Print("7"));
            pt.Add(Print("8"));

            pt.onComplete += () =>
                             {
                                 Application.targetFrameRate = -1;
                                 Debug.Log("Unlock framerate");
                             };
			
            StartCoroutine(pt);
        }
	
        IEnumerator Print(string i)
        {
            Debug.Log(i);
            yield return null;
        }
	
        IEnumerator DoSomethingAsynchonously()  //this can be awfully slow, I suppose it is synched with the frame rate
        {
            for (i = 0; i < 500; i++)
                yield return i; //it will continue on the next frame
		
            Debug.Log("index " + i);
        }
	
        IEnumerator WWWTest()
        {
            WWW www = new WWW("www.google.com");
		
            yield return new WWWEnumerator(www);
		
            Debug.Log("www done:" + www.text);
        }
    }
}
