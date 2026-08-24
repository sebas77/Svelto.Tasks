#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    internal class TaskProfilerMenuItem
    {
        [MenuItem("Tasks/Enable Profiler")]
        public static void EnableProfiler()
        {
            AddScriptingDefineSymbolToAllTargets(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), "TASKS_PROFILER_ENABLED");
        }

        [MenuItem("Tasks/Disable Profiler")]
        public static void DisableProfiler()
        {
            RemoveScriptingDefineSymbolFromAllTargets(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), "TASKS_PROFILER_ENABLED");
        }

        public static void AddScriptingDefineSymbolToAllTargets(NamedBuildTarget group, string defineSymbol)
        {
            var defineSymbols = PlayerSettings.GetScriptingDefineSymbols(group).Split(';').Select(d => d.Trim())
                .ToList();
            if (!defineSymbols.Contains(defineSymbol))
            {
                defineSymbols.Add(defineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(group, string.Join(";", defineSymbols.ToArray()));
            }
        }

        public static void RemoveScriptingDefineSymbolFromAllTargets(NamedBuildTarget group, string defineSymbol)
        {
            var defineSymbols = PlayerSettings.GetScriptingDefineSymbols(group).Split(';').Select(d => d.Trim())
                .ToList();
            if (defineSymbols.Contains(defineSymbol))
            {
                defineSymbols.Remove(defineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(group, string.Join(";", defineSymbols.ToArray()));
            }
        }
    }
}
#endif