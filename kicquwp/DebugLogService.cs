using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace kicquwp
{
    public static class DebugLogService
    {
        private static readonly List<string> _logs = new List<string>();
        private static readonly object _lock = new object();
        private const int MaxLines = 2000;

        public static event Action LogUpdated;

        public static void Log(string message)
        {
            lock (_lock)
            {
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
                _logs.Add(line);
                if (_logs.Count > MaxLines)
                    _logs.RemoveAt(0);
            }
            Debug.WriteLine(message);
            LogUpdated?.Invoke();
        }

        public static string GetFullLog()
        {
            lock (_lock)
            {
                return string.Join("\n", _logs);
            }
        }

        public static void Clear()
        {
            lock (_lock) { _logs.Clear(); }
            LogUpdated?.Invoke();
        }
    }
}