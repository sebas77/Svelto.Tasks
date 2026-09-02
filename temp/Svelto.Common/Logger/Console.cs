#if !DEBUG || PROFILE_SVELTO
#define DISABLE_DEBUG
#endif
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
#if NETFX_CORE
using Windows.System.Diagnostics;
#else
using System.Diagnostics;
#endif
using Svelto.DataStructures;
using Svelto.Utilities;

namespace Svelto
{
    public static partial class Console
    {
        static readonly HashSet<Type> _loggersType;
        static readonly FasterList<ILogger>        _loggers;

        public static bool batchLog = false;

        static Console()
        {
            StringBuilder ValueFactory() => new StringBuilder();

            _stringBuilders = new ThreadLocal<StringBuilder>(ValueFactory);
            _loggers           = new FasterList<ILogger>();
            _loggersType = new HashSet<Type>();
            #if UNITY_5_3_OR_NEWER
            DefaultUnityLogger.Init(); //Ensure Svelto logs have a Unity-backed default sink on first use.
            #else
            SimpleLogger.Init();
            #endif
        }

        public static void AddLogger<T>(T log) where T:ILogger
        {
            if (!_loggersType.Add(typeof(T)))
                return;

            _loggers.Add(log);

            log.OnLoggerAdded();
        }

        public static void Log(string txt)
        {
            InternalLog(txt, LogType.Log, false);
        }

        [Conditional("DEBUG")]
        public static void LogDebug(string txt)
        {
            InternalLog(txt, LogType.LogDebug, false);
        }

        [Conditional("DEBUG")]
        public static void LogDebugWarning(string txt)
        {
            InternalLog(txt, LogType.Warning, false);
        }

        [Conditional("DEBUG")]
        public static void LogDebugWarning(bool assertion, string txt)
        {
            if (assertion == false)
                InternalLog(txt, LogType.Warning, false);
        }

        public static void LogError(string txt, Dictionary<string, string> extraData = null)
        {
            var builder = stringBuilder;

            builder.Length = 0;
            builder.Append("-!!!!!!-> ").Append(txt);

            var toPrint = builder.ToString();

            InternalLog(toPrint, LogType.Error, true, null, extraData);
        }

        public static void LogException(Exception exception, string message = null,
            Dictionary<string, string> extraData = null)
        {
            if (extraData == null)
                extraData = new Dictionary<string, string>();

            string toPrint = "-!!!!!!-> ";

            Exception tracingE = exception;
            int level = 0;
            while (tracingE.InnerException != null)
            {
                tracingE = tracingE.InnerException;

                InternalLog($"-!!!!!!->Internal Exception - Level [{level++}] ", LogType.Exception, false, tracingE);
            }
            
            var builder = stringBuilder;
            builder.Length = 0;
            builder.Append(toPrint).Append(exception.Message);

            if (message != null)
            {
                builder.Append(" -- ").Append(message);

                toPrint = builder.ToString();
            }
 
            //the goal of this is to show the stack from the real error
            InternalLog(toPrint, LogType.Exception, true, exception, extraData);
        }

        public static void LogWarning(string txt)
        {
            var builder = stringBuilder;
            builder.Length = 0;
            builder.Append("------> ").Append(txt);

            var toPrint = builder.ToString();

            InternalLog(toPrint, LogType.Warning, false);
        }

        /// <summary>
        /// this class methods can use only InternalLog to log and cannot use the public methods, otherwise the
        /// stack depth will break 
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="type"></param>
        /// <param name="showLogStack"></param>
        /// <param name="e"></param>
        /// <param name="extraData"></param>
        /// <param name="externalException"></param>
        /// <param name="b"></param>
        static void InternalLog(string txt, LogType type, bool showLogStack = true, Exception e = null,
            Dictionary<string, string> extraData = null)
        {
            for (int i = 0; i < _loggers.count; i++)
                _loggers[i].Log(txt, type, showLogStack, e, extraData);

            var currentLogMessage = logMessage;
            if (currentLogMessage != null)
                currentLogMessage(txt, type, e);

            if (type == LogType.Exception)
            {
                var currentOnException = onException;
                if (currentOnException != null)
                    currentOnException(e, txt);
            }
        }

        public static void CompressLogsToZipAndShow(string zipName)
        {
            for (int i = 0; i < _loggers.count; i++)
                _loggers[i]?.CompressLogsToZipAndShow(zipName);
        }

        internal static StringBuilder stringBuilder
        {
            get
            {
                try
                {
                    return _stringBuilders.Value;
                }
                catch
                {
                    return new StringBuilder(); //this is just to handle finalizer that could be called after the _stringBuilders is finalized. So pretty rare
                }
            }
        }

        public static event Action<string, LogType, Exception> logMessage;
        public static event Action<Exception, string> onException;

        static readonly ThreadLocal<StringBuilder> _stringBuilders;
    }
}
