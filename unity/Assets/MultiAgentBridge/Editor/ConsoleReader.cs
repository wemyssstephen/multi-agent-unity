using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MultiAgentBridge
{
    [InitializeOnLoad]
    public static class ConsoleReader
    {
        private static readonly List<string> logBuffer = new();
        private static readonly object bufferLock = new();
        private const int MaxEntries = 500;

        static ConsoleReader()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private static void HandleLog(string message, string stackTrace, LogType type)
        {
            bool isError = type == LogType.Error
                        || type == LogType.Exception
                        || type == LogType.Assert;

            string entry = isError
                ? $"[{type}] {message}\n{stackTrace}"
                : $"[{type}] {message}";

            lock (bufferLock)
            {
                logBuffer.Add(entry);
                if (logBuffer.Count > MaxEntries)
                {
                    logBuffer.RemoveAt(0);
                }
            }
        }

        public static string GetLogs()
        {
            lock (bufferLock)
            {
                return string.Join("\n", logBuffer);
            }
        }
    }
}