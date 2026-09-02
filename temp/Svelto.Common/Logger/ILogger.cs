using System;
using System.Collections.Generic;

namespace Svelto.Utilities
{
    public enum LogType
    {
        Log,
        Exception,
        Warning,
        Error,
        LogDebug
    }
    public interface ILogger
    {
        void Log(string txt, LogType type = LogType.Log, bool showLogStack = true, Exception e = null,
            Dictionary<string, string> data = null);

        void OnLoggerAdded();
        
        void CompressLogsToZipAndShow(string zipName);
    }
}