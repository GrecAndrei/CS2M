using System;

namespace CS2M.API.Networking
{
    /// <summary>
    ///     Represents the current state of a connection
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        ///     Not connected
        /// </summary>
        Disconnected,
        
        /// <summary>
        ///     Initializing connection components
        /// </summary>
        Initializing,
        
        /// <summary>
        ///     Components initialized, ready to connect
        /// </summary>
        Initialized,
        
        /// <summary>
        ///     Attempting to establish connection
        /// </summary>
        Connecting,
        
        /// <summary>
        ///     Connected and authenticated
        /// </summary>
        Connected,
        
        /// <summary>
        ///     Performing NAT hole punch
        /// </summary>
        NatHolePunching,
        
        /// <summary>
        ///     Acting as server
        /// </summary>
        ServerRunning,
        
        /// <summary>
        ///     Connection failed or error occurred
        /// </summary>
        Failed,
        
        /// <summary>
        ///     Gracefully shutting down
        /// </summary>
        Disconnecting
    }
    
    /// <summary>
    ///     Extension methods for ConnectionState
    /// </summary>
    public static class ConnectionStateExtensions
    {
        /// <summary>
        ///     Check if connection is active (not disconnected)
        /// </summary>
        public static bool IsConnected(this ConnectionState state)
        {
            return state == ConnectionState.Connected || 
                   state == ConnectionState.ServerRunning;
        }
        
        /// <summary>
        ///     Check if connection is in transitional state
        /// </summary>
        public static bool IsTransitional(this ConnectionState state)
        {
            return state == ConnectionState.Connecting || 
                   state == ConnectionState.NatHolePunching ||
                   state == ConnectionState.Initializing;
        }
        
        /// <summary>
        ///     Get human-readable description
        /// </summary>
        public static string ToFriendlyString(this ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Initializing => "Initializing...",
                ConnectionState.Initialized => "Ready to connect",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Connected => "Connected",
                ConnectionState.NatHolePunching => "NAT Punching...",
                ConnectionState.ServerRunning => "Server Running",
                ConnectionState.Failed => "Connection Failed",
                ConnectionState.Disconnecting => "Disconnecting...",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        ///     Check if safe to perform operations
        /// </summary>
        public static bool CanPerformOperations(this ConnectionState state)
        {
            return state == ConnectionState.Connected || 
                   state == ConnectionState.ServerRunning;
        }
    }
}
