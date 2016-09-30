using System.Collections;
using Svelto.Tasks;
using UnityEngine;

public class ExampleParallelTasks : MonoBehaviour 
{
	int i;
	
	// Use this for initialization
	void Start () 
	{
		Application.targetFrameRate = 20;
		
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
	        yield return i;
		
		Debug.Log("index " + i);
	}
	
	IEnumerator WWWTest()
	{
		WWW www = new WWW("www.google.com");
		
		yield return new WWWEnumerator(www);
		
		Debug.Log("www done:" + www.text);
	}
}
