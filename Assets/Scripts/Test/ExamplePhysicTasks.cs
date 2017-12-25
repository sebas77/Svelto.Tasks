using System;
using System.Collections;
using Svelto.Tasks;
using UnityEngine;

namespace Test.Editor
{
    public class ExamplePhysicTasks : MonoBehaviour 
    {
        [TextArea]
        public string Notes = "This example shows how to run a task on the physic scheduler.";

        void OnEnable () 
        {
            UnityConsole.Clear();

            Time.fixedDeltaTime = 0.5f;

            TaskRunner.Instance.RunOnSchedule(StandardSchedulers.physicScheduler, PrintTime);
        }

        void OnDisable()
        {
            StandardSchedulers.physicScheduler.StopAllCoroutines();
        }

        IEnumerator PrintTime()
        {
            var timeNow = DateTime.Now;
            while (true)
            {
                Debug.Log("FixedUpdate time :" + (DateTime.Now - timeNow).TotalSeconds);
                timeNow = DateTime.Now;
                yield return null;
            }
        }
    }
}
