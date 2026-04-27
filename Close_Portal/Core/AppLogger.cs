using System;
using System.IO;
using System.Text;
using System.Web;

namespace Close_Portal.Core {
    public static class AppLogger {

        private static readonly object _lock = new object();

        private static string LogDir =>
            Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "logs");

        public static void Error(string context, Exception ex) =>
            Write("ERROR", context, ex.ToString());

        public static void Error(string context, string message) =>
            Write("ERROR", context, message);

        public static void Warn(string context, string message) =>
            Write("WARN ", context, message);

        public static void Info(string context, string message) =>
            Write("INFO ", context, message);

        private static void Write(string level, string context, string message) {
            try {
                string dir = LogDir;
                Directory.CreateDirectory(dir);
                string file  = Path.Combine(dir, $"app-{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {context} | {message}{Environment.NewLine}";
                lock (_lock) {
                    File.AppendAllText(file, entry, Encoding.UTF8);
                }
            } catch {
                // Never throw from logger
            }
        }
    }
}
