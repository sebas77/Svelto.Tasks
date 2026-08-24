#if UNITY_5_3_OR_NEWER || UNITY_5
using System;
using System.Text.RegularExpressions;
using Svelto.Utilities;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using LogType = UnityEngine.LogType;

namespace Svelto
{
    public static partial class Console
    {
        public class SveltoCatchEmAllConsoleLogHandler: ILogHandler
        {
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                for (var index = 0; index < args.Length; index++)
                {
                    //there have been cases of args having replacement fields, this apparently would break string.Format
                    var arg = args[index];
                    if (arg is string sarg)
                    {
                        //https://sharplab.io/#v2:EYLgdgpgLgZgHgGiiATgVzAHwAICYAMAsAFB4CMJJAbgIYoAEAzlAwLz3Zn4B0AYgPYoAtjSgAKAEQBvfAF8JCehIA8AY34AbQawDuACwCWUCAD4A4luA0NIegBl+q6wYBeog/zD0NNMAHM0Gj8IegByAFF/DQNGPVD6GPohGMYDf3oaehZfRh8oDy8YQXoAawgATzDsowB9FAhU5iE0DSgamAN6mvwyGplZUIBCZQB6dS0UEwkASgBuEk5uAGFPRk0IbgB1FCMIMWYUOaA=
                        args[index] = Regex.Replace(sarg, "{[^}]*}", "");
                    }
                }

                var message = string.Format(format, args);

                switch (logType)
                {
                    case LogType.Error:
                        LogError(message);
                        break;
                    case LogType.Assert:
                        LogError(message);
                        break;
                    case LogType.Warning:
                        LogWarning(message);
                        break;
                    case LogType.Log:
                        Log(message);
                        break;
                    case LogType.Exception:
                        LogError(message);
                        break;
                }
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                Console.LogException(exception);
            }
        }

        /// <summary>
        /// Replaces Unity's global log handler with the Svelto bridge and stores the previous Unity handler so
        /// Svelto loggers can still forward to the real downstream sink. Order matters if other systems also
        /// replace Debug.unityLogger.logHandler.
        /// </summary>
        static void InstallUnityLoggerReplacement()
        {
            if (_initialized == false)
            {
#if UNITY_EDITOR
                _originals = (Application.GetStackTraceLogType(LogType.Warning),
                    Application.GetStackTraceLogType(LogType.Assert), Application.GetStackTraceLogType(LogType.Error),
                    Application.GetStackTraceLogType(LogType.Log), Application.GetStackTraceLogType(LogType.Exception));
#endif
                //CatchEmAll is designed to completely replace the Unity Logger, so we don't need its stack anymore
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

                previousLogHandler = Debug.unityLogger.logHandler;
                Debug.unityLogger.logHandler = sveltoCatchEmAllConsoleLogHandler;
                
#if UNITY_EDITOR
                EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
#endif
                _initialized = true;
            }
        }
        
        public static ILogHandler previousLogHandler;
        public static ILogHandler sveltoCatchEmAllConsoleLogHandler = new SveltoCatchEmAllConsoleLogHandler();

        public static class FasterLog
        {
            public static void UseGlobally(bool replaceUnityLogger)
            {
                DefaultUnityLogger.Init(); //Ensure the default Unity-backed logger is registered before optional replacement/additional loggers.

                if (replaceUnityLogger)
                    InstallUnityLoggerReplacement();

                try
                {
                    FasterUnityLogger.Init();
                }
                catch (Exception e)
                {
                    LogException(e, "something went wrong when initializing FasterLog");
                }
            }
        }
        
        public static class DefaultLog
        {
            public static void ReplaceUnityLogger(bool keepLogHandlerInEditor = false)
            {
                DefaultUnityLogger.Init(); //Ensure the default Unity-backed logger is registered before replacing Unity's handler.

                InstallUnityLoggerReplacement();
                
                _keepLogHandler = keepLogHandlerInEditor;
            }
        }
        
#if UNITY_EDITOR
        static void OnEditorChangedMode(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredEditMode && _initialized && _keepLogHandler == false)
            {
                Debug.unityLogger.logHandler = previousLogHandler;

                Application.SetStackTraceLogType(LogType.Exception, _originals.exception);
                Application.SetStackTraceLogType(LogType.Warning, _originals.warning);
                Application.SetStackTraceLogType(LogType.Assert, _originals.assert);
                Application.SetStackTraceLogType(LogType.Error, _originals.error);
                Application.SetStackTraceLogType(LogType.Log, _originals.log);
                
                EditorApplication.playModeStateChanged -= EditorApplicationOnplayModeStateChanged;
                previousLogHandler = null;
                _initialized = false;
            }
        }
        
        static readonly Action<PlayModeStateChange> EditorApplicationOnplayModeStateChanged = OnEditorChangedMode;
        
        static (StackTraceLogType warning, StackTraceLogType assert, StackTraceLogType error, StackTraceLogType log, StackTraceLogType exception)
                _originals;
#endif 

        static bool _initialized;
        static bool _keepLogHandler;
    }
}
#endif
