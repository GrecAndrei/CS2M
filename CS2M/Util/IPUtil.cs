using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CS2M.Networking;
using LiteNetLib;

namespace CS2M.Util
{
    /// <summary>
    ///     Enhanced IP utility with caching, validation, and error handling
    /// </summary>
    public static class IPUtil
    {
        // Cache for resolved addresses (prevents repeated DNS lookups)
        private static readonly ConcurrentDictionary<string, IPAddress> _dnsCache = new();
        private const int CACHE_TTL_SECONDS = 300; // 5 minutes
        
        // Track last cache clear time
        private static DateTime _lastCacheClear = DateTime.UtcNow;
        private const int CACHE_PURGE_INTERVAL_MINUTES = 60;
        
        /// <summary>
        ///     Create IPEndPoint from hostname/IP and port
        /// </summary>
        public static IPEndPoint CreateIPEndPoint(string hostAddress, int port) 
        {
            if (string.IsNullOrWhiteSpace(hostAddress))
                throw new ArgumentException("Host address cannot be null or empty", nameof(hostAddress));
            
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");
            
            try
            {
                var address = ResolveAddress(hostAddress);
                return new IPEndPoint(address, port);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to resolve address '{hostAddress}': {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        ///     Create IPv4 endpoint with explicit type
        /// </summary>
        public static IPEndPoint CreateIP4EndPoint(string hostAddress, int port)
        {
            try
            {
                var address = ResolveIPv4Only(hostAddress);
                return new IPEndPoint(address, port);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to resolve IPv4 address '{hostAddress}': {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        ///     Resolve hostname to IP address (with caching)
        /// </summary>
        public static IPAddress ResolveAddress(string hostname)
        {
            ValidateHostname(hostname);
            
            // Purge old cache entries periodically
            PurgeOldCacheEntries();
            
            // Try cached entry first
            if (_dnsCache.TryGetValue(hostname, out var cached))
            {
                Log.Trace($"DNS cache hit for '{hostname}'");
                return cached;
            }
            
            // Resolve and cache
            var resolved = Dns.GetHostEntry(hostname).AddressList[0];
            
            if (_dnsCache.TryAdd(hostname, resolved))
            {
                Log.Trace($"DNS resolved: {hostname} -> {resolved}");
            }
            
            return resolved;
        }
        
        /// <summary>
        ///     Resolve only IPv4 address
        /// </summary>
        public static IPAddress ResolveIPv4Only(string hostname)
        {
            ValidateHostname(hostname);
            
            var hostEntry = Dns.GetHostEntry(hostname);
            
            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    Log.Trace($"IPv4 resolved: {hostname} -> {address}");
                    return address;
                }
            }
            
            throw new SocketException((int)SocketError.HostNotFound);
        }
        
        /// <summary>
        ///     Validate hostname format
        /// </summary>
        private static void ValidateHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be null or empty", nameof(hostname));
            
            if (hostname.Length > 253)
                throw new ArgumentException("Hostname too long (max 253 characters)", nameof(hostname));
            
            // Basic validation - allows dots, hyphens, alphanumeric
            foreach (char c in hostname)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                {
                    throw new ArgumentException($"Invalid character in hostname: '{c}'", nameof(hostname));
                }
            }
        }
        
        /// <summary>
        ///     Purge cache entries older than TTL
        /// </summary>
        private static void PurgeOldCacheEntries()
        {
            var now = DateTime.UtcNow;
            
            if ((now - _lastCacheClear).TotalMinutes < CACHE_PURGE_INTERVAL_MINUTES)
                return;
            
            _lastCacheClear = now;
            
            // Simple approach: clear all and rebuild as needed
            // Better implementations would timestamp each entry
            _dnsCache.Clear();
            
            Log.Debug("DNS cache purged (TTL expired)");
        }
        
        /// <summary>
        ///     Format IPEndPoint as string
        /// </summary>
        public static string FormatEndPoint(IPEndPoint endPoint)
        {
            if (endPoint == null)
                return "<null>";
            
            return $"{endPoint.Address}:{endPoint.Port}";
        }
        
        /// <summary>
        ///     Parse string endpoint (host:port) to IPEndPoint
        /// </summary>
        public static IPEndPoint ParseEndPoint(string endPointString)
        {
            if (string.IsNullOrWhiteSpace(endPointString))
                throw new ArgumentException("Endpoint string cannot be null or empty", nameof(endPointString));
            
            string[] parts = endPointString.Split(':');
            
            if (parts.Length != 2)
                throw new ArgumentException("Endpoint must be in format 'host:port'", nameof(endPointString));
            
            if (!int.TryParse(parts[1], out int port))
                throw new ArgumentException($"Invalid port number: '{parts[1]}'", nameof(endPointString));
            
            return CreateIPEndPoint(parts[0], port);
        }
        
        /// <summary>
        ///     Get local IP address
        /// </summary>
        public static IPAddress GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Skip loopback
                    if (!ip.Equals(IPAddress.Loopback))
                        return ip;
                }
            }
            
            // Fallback to any
            return IPAddress.Any;
        }
        
        /// <summary>
        ///     Check if address is local
        /// </summary>
        public static bool IsLocalAddress(IPAddress address)
        {
            if (address == null)
                return false;
            
            return address.Equals(IPAddress.Loopback) ||
                   address.Equals(IPAddress.None) ||
                   address.Equals(IPAddress.Any);
        }
    }
}