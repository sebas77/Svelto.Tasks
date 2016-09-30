using System.Collections;
using UnityEngine;

namespace Assets
{
    sealed public class DoSomethingHeavy2 : MonoBehaviour
    {
        void Awake()
        {
            TaskRunner.Instance.Run(UpdateIt2());
        }
      
        IEnumerator UpdateIt2()
        {
            while (true) 
            {
                Profiler.BeginSample("TaskRunner");
                transform.Translate(0.01f, 0, 0);
                //FindPrimeNumber(1);
                Profiler.EndSample();
                yield return null;
            }
        }
    }
}
