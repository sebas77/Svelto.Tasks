using UnityEngine;

namespace Assets
{
    public class DoSomethingHeavyWithUnity:MonoBehaviour
    {
        void Awake()
        {
            _direction = new Vector2(Mathf.Cos(Random.Range(0, 3.14f)) / 1000, Mathf.Sin(Random.Range(0, 3.14f) / 1000));
            _transform = transform;
        }

        void OnEnable()
        {
            StartCoroutine(UpdateIt2());
        }

        System.Collections.IEnumerator UpdateIt2()
        {
            while (true) 
            {
                _transform.Translate(_direction);

                yield return null;
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        Vector3 _direction;
        Transform _transform;
    }
}
