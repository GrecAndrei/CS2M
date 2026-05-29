using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace CS2M.Util
{
    /// <summary>
    ///     Comprehensive version management and comparison utilities
    /// </summary>
    public static class VersionUtil
    {
        private const string CURRENT_MOD_VERSION = "2.0.0";
        
        /// <summary>
        ///     Get current mod version as simple Version object
        /// </summary>
        public static Version GetModVersion()
        {
            return Assembly.GetAssembly(typeof(VersionUtil)).GetName().Version;
        }

        /// <summary>
        ///     Get game version using Colossal's internal versioning
        /// </summary>
        public static Colossal.Version GetGameVersion()
        {
            return Game.Version.current;
        }
        
        /// <summary>
        ///     Get detailed mod version information
        /// </summary>
        public static ModVersionInfo GetDetailedModVersion()
        {
            var assembly = typeof(Mod).Assembly;
            var asmVersion = assembly.GetName().Version;
            
            return new ModVersionInfo
            {
                AssemblyVersion = asmVersion.ToString(),
                AssemblyFileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion,
                ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "CS2M",
                CompanyName = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "",
                Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "",
                IsRelease = !asmVersion.Build.Equals(-1) || !asmVersion.Revision.Equals(-1)
            };
        }
        
        /// <summary>
        ///     Get game version string
        /// </summary>
        public static string GetGameVersionString()
        {
            try
            {
                // Try to get from Colossal Version if available
                var gameVersionType = Type.GetType("Colossal.Version, Game");
                if (gameVersionType != null)
                {
                    var currentProp = gameVersionType.GetProperty("current");
                    if (currentProp != null)
                    {
                        var currentInstance = currentProp.GetValue(null);
                        var versionProp = currentInstance.GetType().GetProperty("version");
                        if (versionProp != null)
                        {
                            return versionProp.GetValue(currentInstance)?.ToString() ?? Application.version;
                        }
                    }
                }
                
                // Fallback to Unity application version
                return Application.version;
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to get game version: {ex.Message}");
                return "unknown";
            }
        }
        
        /// <summary>
        ///     Check if version string represents a valid version
        /// </summary>
        public static bool IsValidVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return false;
            
            try
            {
                var parts = versionString.Split('.');
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out _))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        ///     Compare two version strings
        /// </summary>
        public static int CompareVersions(string v1, string v2)
        {
            var parsedV1 = ParseVersion(v1);
            var parsedV2 = ParseVersion(v2);
            
            if (parsedV1.Major != parsedV2.Major)
                return parsedV1.Major.CompareTo(parsedV2.Major);
            
            if (parsedV1.Minor != parsedV2.Minor)
                return parsedV1.Minor.CompareTo(parsedV2.Minor);
            
            if (parsedV1.Build != parsedV2.Build)
                return parsedV1.Build.CompareTo(parsedV2.Build);
            
            if (parsedV1.Revision != parsedV2.Revision)
                return parsedV1.Revision.CompareTo(parsedV2.Revision);
            
            return 0;
        }
        
        /// <summary>
        ///     Check if version is compatible with minimum requirement
        /// </summary>
        public static bool IsVersionCompatible(string version, string minVersion)
        {
            return CompareVersions(version, minVersion) >= 0;
        }
        
        /// <summary>
        ///     Parse version into components
        /// </summary>
        private static VersionComponents ParseVersion(string version)
        {
            var parts = version.Split('.');
            
            int major = 0, minor = 0, build = 0, revision = 0;
            
            if (parts.Length > 0 && int.TryParse(parts[0], out var m))
                major = m;
            
            if (parts.Length > 1 && int.TryParse(parts[1], out var m2))
                minor = m2;
            
            if (parts.Length > 2 && int.TryParse(parts[2], out var m3))
                build = m3;
            
            if (parts.Length > 3 && int.TryParse(parts[3], out var m4))
                revision = m4;
            
            return new VersionComponents { Major = major, Minor = minor, Build = build, Revision = revision };
        }
        
        /// <summary>
        ///     Generate unique session ID
        /// </summary>
        public static string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        }
        
        /// <summary>
        ///     Information about mod version
        /// </summary>
        public struct ModVersionInfo
        {
            public string AssemblyVersion;
            public string AssemblyFileVersion;
            public string ProductName;
            public string CompanyName;
            public string Description;
            public bool IsRelease;
        }
        
        /// <summary>
        ///     Parsed version components
        /// </summary>
        private struct VersionComponents
        {
            public int Major;
            public int Minor;
            public int Build;
            public int Revision;
        }
    }
}