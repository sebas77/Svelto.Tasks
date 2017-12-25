using PerformanceMT;
using UnityEngine;

namespace Test.Editor.PerformanceMT
{
    public class SpawnObjectsMT : MonoBehaviour
    {
        [TextArea]
        public string Notes = "Enable this to run the example on another thread.";
        
        // Use this for initialization
        void OnEnable()
        {
            GetComponent<SpawnObjects>().enabled = false;

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            for (int i = 0; i < 150; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                sphere.AddComponent<DoSomethingHeavyMT>();

                sphere.transform.parent = this.transform;
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
