#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    internal class TaskProfilerMenuItem
    {
        [MenuItem("Tasks/Enable Profiler")]
        public static void EnableProfiler()
        {
            AddScriptingDefineSymbolToAllTargets(BuildTargetGroup.Standalone, "TASKS_PROFILER_ENABLED");
        }

        [MenuItem("Tasks/Disable Profiler")]
        public static void DisableProfiler()
        {
            RemoveScriptingDefineSymbolFromAllTargets(BuildTargetGroup.Standalone, "TASKS_PROFILER_ENABLED");
        }

        public static void AddScriptingDefineSymbolToAllTargets(BuildTargetGroup group, string defineSymbol)
        {
            {
                var defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Select(d => d.Trim()).ToList();
                if (!defineSymbols.Contains(defineSymbol))
                {
                    defineSymbols.Add(defineSymbol);
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defineSymbols.ToArray()));
                }
            }
        }

        public static void RemoveScriptingDefineSymbolFromAllTargets(BuildTargetGroup group, string defineSymbol)
        {
            {
                var defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').Select(d => d.Trim()).ToList();
                if (defineSymbols.Contains(defineSymbol))
                {
                    defineSymbols.Remove(defineSymbol);
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defineSymbols.ToArray()));
                }
            }
        }
    }
}
#endif