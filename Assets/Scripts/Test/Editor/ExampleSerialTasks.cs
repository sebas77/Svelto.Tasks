using System.Collections;
using Svelto.Tasks;
using UnityEngine;

public class ExampleSerialTasks : MonoBehaviour 
{
    [TextArea]
    public string Notes = "This example shows how to run different types of tasks in Serial with the TaskRunner";

    void OnEnable()
    {
        UnityConsole.Clear();
    }

    void Start () 
	{
        SerialTaskCollection st = new SerialTaskCollection();
		
		st.Add(Print(1));
		st.Add(Print(2));
		st.Add(DoSomethingAsynchonously(1));
		st.Add(Print(3));
		st.Add(DoSomethingAsynchonously(5));
		st.Add(Print(4));
		st.Add(WWWTest ());
		st.Add(Print(5));
		st.Add(Print(6));

        TaskRunner.Instance.Run(st);
	}
	
	IEnumerator Print(int i)
	{
		Debug.Log(i);
		yield return null;
	}
	
	IEnumerator DoSomethingAsynchonously(float time)
	{
		yield return new WaitForSeconds(time);
		
		Debug.Log("waited " + time);
	}
	
	IEnumerator WWWTest()
	{
		WWW www = new WWW("www.google.com");
		
		yield return new WWWEnumerator(www);
		
		Debug.Log("www done:" + www.text);
	}
}
