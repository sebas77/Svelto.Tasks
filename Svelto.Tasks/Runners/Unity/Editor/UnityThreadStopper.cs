#if UNITY_EDITOR
using UnityEditor;

namespace Svelto.Tasks.Internal
{
#if UNITY_2017_2_OR_NEWER
    [InitializeOnLoad]
    class StopThreadsInEditor
    {
        static StopThreadsInEditor()
        {
            EditorApplication.playModeStateChanged += Update;
        }

        static void Update(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                TaskRunner.StopAndCleanupAllDefaultSchedulers();
            }
        }
    }
#else
    [InitializeOnLoad]
    class StopThreadsInEditor
    {
        static StopThreadsInEditor()
        {
            EditorApplication.playmodeStateChanged += Update;
        }

        static void Update()
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                TaskRunner.StopAndCleanupAllDefaultSchedulers();
            }
        }
    }
#endif
}
#endif