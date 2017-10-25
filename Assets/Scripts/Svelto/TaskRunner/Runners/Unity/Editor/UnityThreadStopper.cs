#if UNITY_EDITOR
using UnityEditor;

namespace Svelto.Tasks.Internal
{
#if UNITY_2017_2
    [InitializeOnLoad]
    class MyClass
    {
        static MyClass()
        {
            EditorApplication.playModeStateChanged += Update;

        }

        static void Update(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                StandardSchedulers.multiThreadScheduler.StopAllCoroutines();
        }
    }
#else
    [InitializeOnLoad]
        class MyClass
        {
            static MyClass()
            {
                EditorApplication.playmodeStateChanged += Update;
            }

            static void Update()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode == false)
                    StandardSchedulers.multiThreadScheduler.StopAllCoroutines();
            }
        }
#endif
}
#endif