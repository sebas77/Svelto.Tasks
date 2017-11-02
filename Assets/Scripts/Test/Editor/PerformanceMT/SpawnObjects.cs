using PerformanceMT;
using UnityEngine;

namespace Test.Editor.PerformanceMT
{
    public class SpawnObjects : MonoBehaviour
    {
        [TextArea]
        public string Notes = "Enable this to run the example on the main thread.";

        // Use this for initialization
        void OnEnable()
        {
            GetComponent<SpawnObjectsMT>().enabled = false;

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            for (var i = 0; i < 150; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                sphere.AddComponent<DoSomethingHeavy>();

                sphere.transform.parent = transform;
            }
        }

        void OnDisable()
        {
            foreach (Transform trans in transform)
            {
                Destroy(trans.gameObject);
            }
        }
    }
}
