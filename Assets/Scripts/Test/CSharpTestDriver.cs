#region Usings
using UnityEngine;

#endregion

public class CSharpTestDriver : MonoBehaviour
{
	public bool runTests;

	private void Start ()
	{
		if (runTests)
			NUnitLiteUnityRunner.RunTests ();
	}
}
