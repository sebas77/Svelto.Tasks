#if UNITY_5_3_OR_NEWER || UNITY_5
using UnityEngine;

namespace Svelto.ObjectPool
{
    public class MonoBehaviourPool<T> : ObjectPool<T> where T:MonoBehaviour
    {
#if POOL_DEBUGGER
    public MonoBehaviourPool()
    {
        GameObject poolDebugger = new GameObject("MonoBehaviourPoolDebugger");

        poolDebugger.AddComponent<PoolDebugger>().SetPool(this);
    }
#endif
        protected override void OnDispose()
        {
            var values = _recycledPools.GetValues(out var count);
            for (int i = 0; i < count; i++)
            {
                foreach (var obj in values[i])
                    GameObject.Destroy(obj);
            }
        }
    }
}
#endif