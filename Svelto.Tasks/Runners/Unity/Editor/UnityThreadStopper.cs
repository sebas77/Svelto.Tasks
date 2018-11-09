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
            if (state == PlayModeStateChange.ExitingPlayMode
             && StandardSchedulers.multiThreadScheduler != null
             && StandardSchedulers.multiThreadScheduler.isKilled == false)
                StandardSchedulers.multiThreadScheduler.Dispose();
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
            if (EditorApplication.isPlayingOrWillChangePlaymode == false 
                && StandardSchedulers.multiThreadScheduler != null 
                && StandardSchedulers.multiThreadScheduler.isKilled == false)
                StandardSchedulers.multiThreadScheduler.Dispose();
        }
    }
#endif
}
#endif