using System;
using System.Collections.Generic;

namespace Svelto.Utilities
{
    public class SimpleLogger : ILogger
    {
        public void Log(string txt, LogType type = LogType.Log, bool showLogStack = true, Exception e = null,
            Dictionary<string, string> data = null)
        {
            var dataString = string.Empty;

            string stack = null;
            
            if (e != null)
                stack = e.StackTrace;

            if (data != null)
                dataString = DataToString.DetailString(data);
            
            switch (type)
            {
                case LogType.Log:
                    SystemLog(txt);
                    break;
                case LogType.Warning:
                    SystemLog(txt);
                    break;
                case LogType.Error:
                case LogType.LogDebug:
                case LogType.Exception:
                    SystemLog(txt.FastConcat(Environment.NewLine, stack)
                        .FastConcat(Environment.NewLine, dataString));
                    break;
            }
        }

        public void OnLoggerAdded()
        {
        }

        public void CompressLogsToZipAndShow(string zipName)
        {
            
        }

        public static void SystemLog(string txt)
        {
            System.Console.WriteLine(txt);
        }
        
        public static void Init()
        {
            Console.AddLogger(new SimpleLogger());
        }

    }
}