#if UNITY_EDITOR
#if DEBUG && !PROFILE_SVELTO
using UnityEngine;
using UnityEditor;

namespace Svelto.ObjectPool
{
    [CustomEditor(typeof(PoolDebugger))]
    public class PoolDebuggerCustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PoolDebugger myScript = (PoolDebugger) target;
            if (GUILayout.Button("Fetch Object Created"))
            {
                myScript.FetchObjectCreated();
            }

            if (GUILayout.Button("Fetch Object Reused"))
            {
                myScript.FetchObjectReused();
            }

            if (GUILayout.Button("Fetch Object Recycled"))
            {
                myScript.FetchObjectRecycled();
            }
        }
    }
}
#endif
#endif