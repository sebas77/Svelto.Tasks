#if !NETFX_CORE
using UnityEngine;

public class CSharpTestDriver : MonoBehaviour
{
	public bool runTests;

	private void Start ()
	{
		if (runTests)
			NUnitLiteUnityRunner.RunTests ();
	}
}
#endif