using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Message
    {
        public string Destination { get; set; }
        public string RequestType { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
    }
    public class GameMove
    {
        public string Text { get; set; } = "NULL";
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class User
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class GameRoom
    {
        public string Name { get; set; }
        public bool State { get; set; }
        public string MainUser { get; set; }
        public string SubUser { get; set; }
        public bool MainUserReady { get; set; } = false;
        public bool SubUserReady { get; set; } = false;
        public bool CheckReadyToStart()
        {
            return MainUserReady && SubUserReady;
        }
    }

    public class UserAction
    {
        public string RequestType { get; set; }
        public byte x_point { get; set; }
        public byte y_point { get; set; }
    }

    public class Logger
    {
        private static Logger _instance;
        private static object _lock = new object();

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Logger();
                        }
                    }
                }
                return _instance;
            }
        }

        private Logger() { }

        public void Log(LogLevel level, string message)
        {
            string logMessage = FormatLogMessage(level, message);
            Console.WriteLine(logMessage);
        }

        private string FormatLogMessage(LogLevel level, string message)
        {
            StackTrace stackTrace = new StackTrace(2, true);
            StackFrame frame = stackTrace.GetFrame(0);
            string className = frame.GetMethod().DeclaringType.FullName;
            int lineNumber = frame.GetFileLineNumber();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            return $"[{timestamp}] [{level}] {className}:{lineNumber} - {message}";
        }
    }
}
