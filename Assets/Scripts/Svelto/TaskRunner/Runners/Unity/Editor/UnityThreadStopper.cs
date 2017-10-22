using UnityEditor;

namespace Svelto.Tasks.Internal
{
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
                (StandardSchedulers.multiThreadScheduler as MultiThreadRunner).Kill();
        }
    }
}
