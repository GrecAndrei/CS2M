using Colossal.Logging;
using System;
using LiteNetLib;
using System.Threading;
using UnityEngine;

namespace CS2M
{
    /// <summary>
    ///     Enhanced network logger wrapper with better message formatting
    /// </summary>
    public class NetLogWrapper : INetLogger
    {
        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            try
            {
                var formatted = string.Format(str, args);
                
                switch (level)
                {
                    case NetLogLevel.Info:
                        Log.Debug($"[Network Info] {formatted}");
                        break;
                    case NetLogLevel.Warning:
                        Log.Warn($"[Network Warning] {formatted}");
                        break;
                    case NetLogLevel.Error:
                        Log.Error($"[Network Error] {formatted}");
                        break;
                    case NetLogLevel.Trace:
                        Log.Trace($"[Network Trace] {formatted}");
                        break;
                }
            }
            catch (Exception ex)
            {
                // Prevent logging errors from breaking logging
                Debug.LogError($"Failed to process log message: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Comprehensive logging system with multiple levels and contextual support
    /// </summary>
    public static class Log
    {
        private static ILog _logger;
        
        // Thread-local for correlation IDs and context
        [ThreadStatic]
        private static string _currentCorrelationId;
        
        [ThreadStatic]
        private static int _logLevelThreshold;
        
        // Default threshold: INFO (Level.Info)
        private const int DEFAULT_LOG_LEVEL = 2;
        
        /// <summary>
        ///     Initialize logger with proper configuration
        /// </summary>
        public static void Initialize(string modName = nameof(CS2M))
        {
            if (_logger == null)
            {
                _logger = LogManager.GetLogger(modName)
                    .SetShowsErrorsInUI(true)
                    .SetEffectiveness(Level.Info)
                    .SetLogStackTrace(false);
                
                Log.Info("Logging system initialized");
            }
        }

        #region Static Properties
        
        /// <summary>
        ///     Access to underlying Colossal logger
        /// </summary>
        public static ILog Logger => _logger ?? LogManager.GetLogger("CS2M");
        
        /// <summary>
        ///     Current log level threshold
        /// </summary>
        public static int LogLevelThreshold
        {
            get
            {
                if (!IsInitialized())
                    return DEFAULT_LOG_LEVEL;
                
                if (_logLevelThreadLocal == 0)
                    return DEFAULT_LOG_LEVEL;
                    
                return _logLevelThreadLocal;
            }
            set
            {
                _logLevelThreadLocal = value;
                SetLogLevelInternal(Level.GetLevel(value));
            }
        }
        
        private static int _logLevelThreadLocal;
        
        /// <summary>
        ///     Current correlation ID for tracing operations
        /// </summary>
        public static string CorrelationId
        {
            get => _currentCorrelationId;
            set => _currentCorrelationId = value;
        }
        
        /// <summary>
        ///     Check if logging is currently enabled for all levels
        /// </summary>
        public static bool IsEnabled => _logger != null;
        
        /// <summary>
        ///     Get current effective log level
        /// </summary>
        public static Level CurrentLogLevel
        {
            get
            {
                return Level.GetLevel(LogLevelThreshold);
            }
        }
        
        #endregion
        
        #region Configuration Methods
        
        /// <summary>
        ///     Set logging level globally
        /// </summary>
        public static void SetLoggingLevel(Level loggingLevel)
        {
            SetLogLevelInternal(loggingLevel);
            Log.Info($"Logging level set to: {loggingLevel}");
        }
        
        /// <summary>
        ///     Set logging level for current thread only
        /// </summary>
        public static void SetThreadLogLevel(Level loggingLevel)
        {
            _logLevelThreadLocal = loggingLevel.severity;
        }
        
        /// <summary>
        ///     Enable or disable stack traces in error logs
        /// </summary>
        public static void EnableStackTrace(bool enable)
        {
            if (_logger != null)
                _logger.SetLogStackTrace(enable);
        }
        
        /// <summary>
        ///     Reset logger to default settings
        /// </summary>
        public static void Reset()
        {
            _logger?.SetLogStackTrace(false);
            _logger?.SetEffectiveness(Level.Info);
            _logLevelThreadLocal = DEFAULT_LOG_LEVEL;
            _currentCorrelationId = null;
        }
        
        #endregion
        
        #region Core Logging Methods
        
        /// <summary>
        ///     Error level logging with optional exception context
        /// </summary>
        public static void Error(string message)
        {
            if (ShouldLog(Level.Error))
                Logger.Error(message);
        }
        
        /// <summary>
        ///     Error level with exception details and stack trace
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            if (ShouldLog(Level.Error))
                Logger.Error(ex, message);
        }
        
        /// <summary>
        ///     Error level with full stack trace forced
        /// </summary>
        public static void ErrorWithStackTrace(string message)
        {
            if (ShouldLog(Level.Error))
            {
                var stackTrace = System.Environment.StackTrace;
                Logger.Error($"{message}\n{stackTrace}");
            }
        }
        
        /// <summary>
        ///     Warning level logging
        /// </summary>
        public static void Warn(string message)
        {
            if (ShouldLog(Level.Warn))
                Logger.Warn(message);
        }
        
        /// <summary>
        ///     Informational logging
        /// </summary>
        public static void Info(string message)
        {
            if (ShouldLog(Level.Info))
                Logger.Info(message);
        }
        
        /// <summary>
        ///     Debug level logging
        /// </summary>
        public static void Debug(string message)
        {
            if (ShouldLog(Level.Debug))
                Logger.Debug(message);
        }
        
        /// <summary>
        ///     Trace level logging (most verbose)
        /// </summary>
        public static void Trace(string message)
        {
            if (ShouldLog(Level.Trace))
                Logger.Trace(message);
        }
        
        #endregion
        
        #region Context-Aware Logging
        
        /// <summary>
        ///     Log with structured context data
        /// </summary>
        public static void Info(string message, params object[] context)
        {
            var formatted = ShouldLog(Level.Info) 
                ? FormatWithContext(message, context) 
                : message;
            
            if (ShouldLog(Level.Info))
                Logger.Info(formatted);
        }
        
        /// <summary>
        ///     Conditional debug logging (only evaluated if enabled)
        /// </summary>
        public static void WhenDebug(Func<string> getMessage)
        {
            if (ShouldLog(Level.Debug))
            {
                var message = getMessage();
                if (message != null)
                    Logger.Debug(message);
            }
        }
        
        /// <summary>
        ///     Conditional info logging
        /// </summary>
        public static void WhenInfo(Func<string> getMessage)
        {
            if (ShouldLog(Level.Info))
            {
                var message = getMessage();
                if (message != null)
                    Logger.Info(message);
            }
        }
        
        /// <summary>
        ///     Conditional warn logging
        /// </summary>
        public static void WhenWarn(Func<string> getMessage)
        {
            if (ShouldLog(Level.Warn))
            {
                var message = getMessage();
                if (message != null)
                    Logger.Warn(message);
            }
        }
        
        /// <summary>
        ///     Rate-limited logging to prevent spam
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RateLimitState> _rateLimits = new();
        
        /// <summary>
        ///     Log with rate limiting (max once per second by default)
        /// </summary>
        public static void RateLimited(string key, string message, int intervalMs = 1000)
        {
            if (!ShouldLog(Level.Warn))
                return;
            
            var state = _rateLimits.GetOrAdd(key, _ => new RateLimitState());
            
            lock (state)
            {
                if ((DateTime.UtcNow - state.LastTime).TotalMilliseconds > intervalMs)
                {
                    Logger.Warn(message);
                    state.LastTime = DateTime.UtcNow;
                }
            }
        }
        
        #endregion
        
        #region Performance & Metrics
        
        /// <summary>
        ///     Log performance measurement
        /// </summary>
        public static void Measure(string operationName, long milliseconds)
        {
            if (milliseconds > 50) // Warn about slow operations
            {
                Warn($"Slow operation '{operationName}': {milliseconds}ms");
            }
            else if (milliseconds > 10)
            {
                Debug($"Operation '{operationName}': {milliseconds}ms");
            }
        }
        
        /// <summary>
        ///     Timer-based logging helper
        /// </summary>
        public static System.Diagnostics.Stopwatch StartTimer(string operationName)
        {
            Debug($"Starting operation: {operationName}");
            return System.Diagnostics.Stopwatch.StartNew();
        }
        
        #endregion
        
        #region Internal Helpers
        
        private static bool ShouldLog(Level level)
        {
            if (_logger == null)
                return false;
            
            // Apply threshold logic
            var levelValue = level.severity;
            var threshold = LogLevelThreshold;
            
            return levelValue >= threshold;
        }
        
        /// <summary>
        ///     Set log level internally
        /// </summary>
        private static void SetLogLevelInternal(Level level)
        {
            if (_logger != null)
                _logger.SetEffectiveness(level);
        }
        
        /// <summary>
        ///     Check initialization status
        /// </summary>
        private static bool IsInitialized()
        {
            return _logger != null;
        }
        
        /// <summary>
        ///     Format message with context data
        /// </summary>
        private static string FormatWithContext(string message, object[] context)
        {
            if (context == null || context.Length == 0)
                return message;
            
            // Build context string
            var ctxParts = new System.Collections.Generic.List<string>();
            
            foreach (var obj in context)
            {
                if (obj != null)
                {
                    var typeName = obj.GetType().Name;
                    if (obj is System.Collections.IDictionary dict)
                    {
                        ctxParts.Add($"{typeName}{{{dict.Count} pairs}}");
                    }
                    else if (obj is System.Collections.IEnumerable enumerable)
                    {
                        var list = enumerable as System.Collections.IList;
                        ctxParts.Add($"{typeName}[{list?.Count ?? 0} items]");
                    }
                    else
                    {
                        ctxParts.Add($"{typeName}: {obj.ToString()}");
                    }
                }
            }
            
            return $"{message} [{string.Join(", ", ctxParts)}]";
        }
        
        #endregion
        
        #region Supporting Types
        
        /// <summary>
        ///     Rate limit tracking state
        /// </summary>
        private class RateLimitState
        {
            public DateTime LastTime;
        }
        
        #endregion
    }
    
    /// <summary>
    ///     Stopwatch extension for timing operations
    /// </summary>
    public static class StopwatchExtensions
    {
        public static string StartEvent;
        
        public static TimeSpan Elapsed(this System.Diagnostics.Stopwatch sw)
        {
            return sw.Elapsed;
        }
        
        public static void StopAndLog(this System.Diagnostics.Stopwatch sw, string expectedName)
        {
            if (sw.IsRunning)
            {
                sw.Stop();
                Log.Measure(expectedName, sw.ElapsedMilliseconds);
            }
        }
    }
}
