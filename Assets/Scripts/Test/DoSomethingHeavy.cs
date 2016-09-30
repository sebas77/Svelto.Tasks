using UnityEngine;

namespace Assets
{
    public class DoSomethingHeavy:MonoBehaviour
    {
        void Update()
        {
            transform.Translate(0.01f,0,0);
        }
    }
}
