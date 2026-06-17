using Colossal.Logging;
using System;
using LiteNetLib;
using UnityEngine;

namespace CS2M
{
    public class NetLogWrapper : INetLogger
    {
        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            try
            {
                var formatted = string.Format(str, args);
                switch (level)
                {
                    case NetLogLevel.Info: Log.Debug($"[Network Info] {formatted}"); break;
                    case NetLogLevel.Warning: Log.Warn($"[Network Warning] {formatted}"); break;
                    case NetLogLevel.Error: Log.Error($"[Network Error] {formatted}"); break;
                    case NetLogLevel.Trace: Log.Trace($"[Network Trace] {formatted}"); break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to process log message: {ex.Message}");
            }
        }
    }

    public static class Log
    {
        private static ILog _logger;
        private static int _logLevelThreshold = Level.Info.severity;

        public static ILog Logger
        {
            get
            {
                if (_logger == null) Initialize();
                return _logger;
            }
        }

        public static void Initialize(string modName = nameof(CS2M))
        {
            if (_logger != null) return;
            _logger = LogManager.GetLogger(modName)
                .SetShowsErrorsInUI(true)
                .SetEffectiveness(Level.Info)
                .SetLogStackTrace(false);
        }

        public static int LogLevelThreshold
        {
            get => _logLevelThreshold;
            set
            {
                _logLevelThreshold = value;
                _logger?.SetEffectiveness(Level.GetLevel(value));
            }
        }

        public static void SetLoggingLevel(Level level)
        {
            LogLevelThreshold = level.severity;
        }

        private static bool ShouldLog(Level level)
        {
            return _logger != null && level.severity >= _logLevelThreshold;
        }

        public static void Error(string message) { if (ShouldLog(Level.Error)) Logger.Error(message); }
        public static void Error(string message, Exception ex) { if (ShouldLog(Level.Error)) Logger.Error(ex, message); }
        public static void Warn(string message) { if (ShouldLog(Level.Warn)) Logger.Warn(message); }
        public static void Info(string message) { if (ShouldLog(Level.Info)) Logger.Info(message); }
        public static void Debug(string message) { if (ShouldLog(Level.Debug)) Logger.Debug(message); }
        public static void Trace(string message) { if (ShouldLog(Level.Trace)) Logger.Trace(message); }
    }
}
