using UnityEngine;

namespace PerformanceMT
{

    public class SpawnObjectsMT : MonoBehaviour
    {
        // Use this for initialization
        void Start()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            var parent1 = new GameObject();

            for (int i = 0; i < 150; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                sphere.AddComponent<DoSomethingHeavy2>();

                sphere.transform.parent = parent1.transform;
            }
        }
    }
}
