using System.Collections;
using Svelto.Tasks;
using UnityEngine;
using System;

public class ExampleStaggeredTasks : MonoBehaviour 
{
    private const int MaxTasksPerFrame = 3;
    [TextArea]
    public string Notes = "This example shows how to run tasks spreaded over several frames.";

    StaggeredMonoRunner _runner;

    void OnEnable () 
	{
        UnityConsole.Clear();

        _runner = new StaggeredMonoRunner("StaggeredRunner", MaxTasksPerFrame);

        for (int i = 0; i < 300; i++)
            TaskRunner.Instance.RunOnSchedule(_runner, PrintFrame);
	}

    void OnDisable()
    {
        _runner.StopAllCoroutines();
    }

    IEnumerator PrintFrame()
	{
        Debug.Log(Time.frameCount);
        yield break;
	}
}
