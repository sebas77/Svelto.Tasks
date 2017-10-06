using UnityEditor;

namespace Svelto.Tasks.Internal
{
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
}
