using System.Collections;
using UnityEngine;

namespace Test.Editor.UnityVSTaskRunner
{
    public class DoSomethingHeavyWithTaskRunner : MonoBehaviour
    {
        void Awake()
        {
            _direction = new Vector2(Mathf.Cos(Random.Range(0, 3.14f)) / 1000, Mathf.Sin(Random.Range(0, 3.14f) / 1000));
            _transform = this.transform;

            _task = TaskRunner.Instance.AllocateNewTaskRoutine().
                SetEnumeratorProvider(UpdateIt2);
        }

        void OnEnable() 
        {
            _task.Start();
        }
      
        IEnumerator UpdateIt2()
        {
            while (true) 
            {
                _transform.Translate(_direction);

                yield return null;
            }
        }

        void OnDisable()
        {
            _task.Stop();
        }

        Vector3 _direction;
        Transform _transform;
        Svelto.Tasks.ITaskRoutine _task;
    }
}
