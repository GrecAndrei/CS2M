using System;
using UnityEngine;

namespace CS2M.API
{
    /// <summary>
    ///     Logging wrapper for CS2M.API assembly using Unity Debug
    /// </summary>
    public static class Log
    {
        public static void Info(string message) => UnityEngine.Debug.Log($"[CS2M.API] {message}");
        public static void Debug(string message) => UnityEngine.Debug.Log($"[CS2M.API] {message}");
        public static void Warn(string message) => UnityEngine.Debug.LogWarning($"[CS2M.API] {message}");
        public static void Error(string message) => UnityEngine.Debug.LogError($"[CS2M.API] {message}");
        public static void Error(string message, Exception ex) => UnityEngine.Debug.LogError($"[CS2M.API] {message}: {ex.Message}\n{ex.StackTrace}");
        public static void Trace(string message) => UnityEngine.Debug.Log($"[CS2M.API] [Trace] {message}");
    }
}
