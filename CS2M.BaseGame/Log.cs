using Colossal.Logging;
using System;

namespace CS2M.BaseGame.Systems
{
    /// <summary>
    ///     Logging bridge for the BaseGame assembly
    /// </summary>
    public static class Log
    {
        private static ILog _logger;

        private static ILog Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = LogManager.GetLogger("CS2M");
                }
                return _logger;
            }
        }

        public static void Info(string message) => Logger.Info(message);
        public static void Debug(string message) => Logger.Debug(message);
        public static void Warn(string message) => Logger.Warn(message);
        public static void Error(string message) => Logger.Error(message);
        public static void Trace(string message) => Logger.Trace(message);
        public static void Error(string message, Exception ex) => Logger.Error(ex, message);
    }
}
