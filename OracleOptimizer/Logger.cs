using System;
using System.IO;

namespace OracleOptimizer
{
    public static class Logger
    {
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "optimizer_log.txt");
        private static readonly object lockObj = new object();

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogError(string message, Exception ex = null)
        {
            string errorMessage = ex != null ? $"{message}: {ex.ToString()}" : message;
            Log("ERROR", errorMessage);
        }

        private static void Log(string level, string message)
        {
            try
            {
                lock (lockObj)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(logFilePath, logEntry);
                }
            }
            catch (Exception fsEx)
            {
                // Fallback or error handling for logging failure itself
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {fsEx.Message}");
            }
        }
    }
}
