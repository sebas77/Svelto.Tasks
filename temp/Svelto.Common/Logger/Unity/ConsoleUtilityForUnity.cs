#if UNITY_5_3_OR_NEWER || UNITY_5
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Svelto.Utilities
{
    public static class ConsoleUtilityForUnity
    {
        const int MAX_NUMBER_OF_STACK_LINES = 3;

        static ConsoleUtilityForUnity()
        {
            projectFolder = Application.dataPath.Replace("Assets", "");
        }

        public static string LogFormatter(string txt, LogType type, bool showLogStack, Exception e, string frame,
            string dataString, StackTrace stackTrace)
        {
            string stack;

            switch (type)
            {
                case LogType.Log:
                {
#if UNITY_EDITOR
                    stack = (showLogStack ? ExtractFormattedStackTrace(stackTrace) : string.Empty);

                    return ($"{frame} <b><color=teal> ".FastConcat(txt, " </color></b> ", Environment.NewLine, stack)
                           .FastConcat(Environment.NewLine, dataString));
#else
                    return ($"{frame} ".FastConcat(txt).FastConcat(Environment.NewLine, dataString));
#endif
                }
                case LogType.LogDebug:
                {
#if UNITY_EDITOR
                    stack = (showLogStack ? ExtractFormattedStackTrace(stackTrace) : string.Empty);

                    stack = ($"{frame} <b><color=yellow> ".FastConcat(txt, " </color></b> ", Environment.NewLine, stack)
                           .FastConcat(Environment.NewLine, dataString));

                    return stack;
#else
                    return "";
#endif
                }
                case LogType.Warning:
                {
#if UNITY_EDITOR
                    stack = (showLogStack ? ExtractFormattedStackTrace(stackTrace) : string.Empty);

                    stack = ($"{frame} <b><color=orange> ".FastConcat(txt, " </color></b> ", Environment.NewLine, stack)
                           .FastConcat(Environment.NewLine, dataString));

                    return stack;
#else
                   return ($"{frame} ".FastConcat(txt).FastConcat(Environment.NewLine, dataString));
#endif
                }
                case LogType.Error:
                case LogType.Exception:
                {
                    if (e != null)
                    {
                        txt = txt.FastConcat(" ", e.Message.ToString());
                        var trace = new StackTrace(e, true);
                        stack = showLogStack
                                ? ExtractFormattedStackTrace(trace, stackTrace)
                                : ExtractFormattedStackTrace(trace);
                    }
                    else
                        stack = showLogStack ? ExtractFormattedStackTrace(stackTrace) : string.Empty;

#if UNITY_EDITOR
                    stack = ($"{frame} ".FastConcat(txt, " ", Environment.NewLine, stack)
                           .FastConcat(Environment.NewLine, dataString));

                    return stack;
#else
                    stack = $"{frame} ".FastConcat(txt, Environment.NewLine, stack).FastConcat(dataString);
                    return stack;
#endif
                }
            }

            throw new NotImplementedException();
        }

        /// <summary>
        ///     copied from Unity source code, whatever....
        /// </summary>
        /// <param name="trace"></param>
        /// <param name="stackTrace"></param>
        /// <param name="externalStackTrace"></param>
        /// <returns></returns>
        static string ExtractFormattedStackTrace(StackTrace stackTrace)
        {
            var builder = Svelto.Console.stringBuilder;

            builder.Length = 0;

            PrintStack(stackTrace, builder);

            return builder.ToString();
        }

        static string ExtractFormattedStackTrace(StackTrace stackTrace, StackTrace stackTrace1)
        {
            return ExtractFormattedStackTrace(stackTrace)
                   .FastConcat("---------------", Environment.NewLine)
                   .FastConcat(ExtractFormattedStackTrace(stackTrace1));
        }

        static void PrintStack(StackTrace stackTrace, StringBuilder builder)
        {
            var frameCount = Math.Min((int)MAX_NUMBER_OF_STACK_LINES, stackTrace.FrameCount);

            try
            {
                for (var index1 = 0; index1 < frameCount; ++index1)
                {
                    FormatStack(stackTrace.GetFrame(index1), builder);
                }
            }
            catch { }
        }

        static void FormatStack(StackFrame frame, StringBuilder sb)
        {
            MethodBase mb = frame.GetMethod();
            if (mb == null)
                return;

            Type classType = mb.DeclaringType;
            if (classType == null)
                return;

            // Add namespace.classname:MethodName
            String ns = classType.Namespace;
            if (!string.IsNullOrEmpty(ns))
            {
                sb.Append(ns);
                sb.Append(".");
            }

            sb.Append(classType.Name);
            sb.Append(":");
            sb.Append(mb.Name);
            sb.Append("(");

            // Add parameters
            int j = 0;
            ParameterInfo[] pi = mb.GetParameters();
            bool fFirstParam = true;
            while (j < pi.Length)
            {
                if (fFirstParam == false)
                    sb.Append(", ");
                else
                    fFirstParam = false;

                sb.Append(pi[j].ParameterType.Name);
                j++;
            }

            sb.Append(")");

            // Add path name and line number - unless it is a Debug.Log call, then we are only interested
            // in the calling frame.
            string path = frame.GetFileName();
            if (path != null)
            {
                bool shouldStripLineNumbers = (classType.Name == "Debug" && classType.Namespace == "UnityEngine") ||
                        (classType.Name == "Logger" && classType.Namespace == "UnityEngine") ||
                        (classType.Name == "DebugLogHandler" && classType.Namespace == "UnityEngine") ||
                        (classType.Name == "Assert" && classType.Namespace == "UnityEngine.Assertions") ||
                        (mb.Name == "print" && classType.Name == "MonoBehaviour"
                         && classType.Namespace == "UnityEngine");

                if (!shouldStripLineNumbers)
                {
                    sb.Append(" (at");

                    if (!string.IsNullOrEmpty(projectFolder))
                    {
                        if (path.Replace("\\", "/").StartsWith((string)projectFolder))
                        {
                            path = path.Substring(projectFolder.Length, path.Length - projectFolder.Length);
                        }
                    }

#if UNITY_EDITOR
                    sb.Append($" <a href=\"{path}\" line=\"{frame.GetFileLineNumber()}\">");
#endif
                    sb.Append(path);
                    sb.Append(":");
                    sb.Append(frame.GetFileLineNumber().ToString());
#if UNITY_EDITOR
                    sb.Append("</a>)");
#else
                    sb.Append(")");
#endif
                }
            }

            sb.Append("\n");
        }

        static readonly string projectFolder;
    }
}
#endif
