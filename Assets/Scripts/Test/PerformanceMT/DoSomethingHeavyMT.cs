using System.Collections;
using Svelto.Tasks;
using UnityEngine;

namespace PerformanceMT
{
    public class DoSomethingHeavyMT : MonoBehaviour
    {
        Vector2 direction;
        void Start()
        {
            TaskRunner.Instance.RunOnSchedule(StandardSchedulers.multiThreadScheduler, CalculateAndShowNumber());
            direction = new Vector2(Mathf.Cos(Random.Range(0, 3.14f)) / 1000, Mathf.Sin(Random.Range(0, 3.14f) / 1000));
        }

        IEnumerator CalculateAndShowNumber() //this will run on another thread
        {
            while (true)
            {
                IEnumerator enumerator = FindPrimeNumber((rnd1.Next() % 1000));

                yield return enumerator;

                long result = (long)enumerator.Current * 333;

                yield return SetColor(result).ThreadSafeRun(); //yep the thread will wait for this other task to finish on the mainThreadScheduler
            }
        }

        void OnDestroy()
        {
            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();
        }

        IEnumerator SetColor(long result)
        {
            GetComponent<Renderer>().material.color = new Color((result % 255) / 255f, ((result * result) % 255) / 255f, ((result / 44) % 255) / 255f);

            yield return null;
        }

        void OnApplicationQuit()
        {
            StandardSchedulers.StopSchedulers(); //Unity will get stuck for ever if you don't do this
        }

        void OnDisable()
        {
            StandardSchedulers.StopSchedulers(); //Unity will get stuck for ever if you don't do this
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
