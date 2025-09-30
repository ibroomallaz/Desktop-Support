using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Logging
{
    // Static facade: level filtering, settings integration, and clear helpers.
    public static class Log
    {
        public static IAppLogger? Instance { get; private set; }

        private static AppLogLevel _min = AppLogLevel.Warn;
        private static int _retentionDays = 14;

        public static void Initialize(IAppLogger logger, AppLogLevel min = AppLogLevel.Warn, int retentionDays = 14)
        {
            Instance = logger;
            _min = min;
            _retentionDays = retentionDays;
            if (Instance is FileLogger fl) fl.SetRetentionDays(_retentionDays);
        }

        // Call after AppSettings is loaded (and whenever user changes logging prefs)
        public static void ApplySettings(AppSettings s)
        {
            _min = s.Logging.MinimumLevel;
            _retentionDays = s.Logging.RetentionDays;
            if (Instance is FileLogger fl) fl.SetRetentionDays(_retentionDays);
        }

        public static void Debug(string tag, string message) => Write(AppLogLevel.Debug, tag, message);
        public static void Info(string tag, string message) => Write(AppLogLevel.Info, tag, message);
        public static void Warn(string tag, string message) => Write(AppLogLevel.Warn, tag, message);
        public static void Error(string tag, string message, Exception? ex = null) => Write(AppLogLevel.Error, tag, message, ex);

        // Treat “success” as Info-level; whether it lands depends on MinimumLevel.
        public static void Success(string tag, string message) => Info($"SUCCESS:{tag}", message);

        public static void Write(AppLogLevel level, string tag, string message, Exception? ex = null)
        {
            if (_min == AppLogLevel.Off) return;
            if ((int)level < (int)_min) return;
            Instance?.Write(level, tag, message, ex);
        }

        // Convenience for UI actions
        public static void TruncateToday() => (Instance as FileLogger)?.TruncateToday();
        public static void PurgeOlderThan(DateTime cutoffUtc) => (Instance as FileLogger)?.PurgeOlderThan(cutoffUtc);
        public static void PurgeAll(bool includeToday = false) => (Instance as FileLogger)?.PurgeAll(includeToday);

        public static string? LogsDirectoryPath => (Instance as FileLogger)?.DirectoryPath;
    }
}
