using System.Collections;
using UnityEngine;

namespace PerformanceMT
{
    public class DoSomethingHeavy:MonoBehaviour
    {
        Vector2 direction;
        void Start()
        {
            StartCoroutine(CalculateAndShowNumber());
            direction = new Vector2(Mathf.Cos(Random.Range(0, 3.14f)) / 1000, Mathf.Sin(Random.Range(0, 3.14f) / 1000));
        }

        IEnumerator CalculateAndShowNumber()
        {
            while (true)
            {
                IEnumerator enumerator = FindPrimeNumber((rnd1.Next() % 1000));

                yield return enumerator;

                long result = (long)enumerator.Current * 333;

                GetComponent<Renderer>().material.color = new Color((result % 255) / 255f, ((result * result) % 255) / 255f, ((result / 44) % 255) / 255f);
            }
        }

        void Update()
        {
            transform.Translate(direction);
        }

        public IEnumerator FindPrimeNumber(int n)
        {
            int count = 0;
            long a = 2;
            while (count < n)
            {
                long b = 2;
                int prime = 1;// to check if found a prime
                while (b * b <= a)
                {
                    if (a % b == 0)
                    {
                        prime = 0;
                        break;
                    }
                    b++;
                }
                if (prime > 0)
                    count++;
                a++;
            }

            yield return --a;
        }

        static System.Random rnd1 = new System.Random(); //not a problem, multithreaded coroutine are threadsafe within the same runner
    }
}
