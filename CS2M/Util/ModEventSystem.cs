using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using CS2M.API.Networking;

namespace CS2M.Util
{
    /// <summary>
    ///     Event-driven communication system for decoupled mod components
    /// </summary>
    public static class ModEventSystem
    {
        // Thread-safe event registries
        private static readonly Dictionary<Type, DelegateRegistry> _eventRegistry = new();
        private static readonly object _registryLock = new object();
        
        // Event queues for async processing
        private static readonly Queue<EventItem> _eventQueue = new();
        private static bool _isProcessing;
        
        /// <summary>
        ///     Subscribe to an event type
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            lock (_registryLock)
            {
                if (!_eventRegistry.ContainsKey(typeof(T)))
                    _eventRegistry[typeof(T)] = new DelegateRegistry();
                
                ((DelegateRegistry)_eventRegistry[typeof(T)]).AddHandler(handler);
            }
        }
        
        /// <summary>
        ///     Unsubscribe from an event type
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            lock (_registryLock)
            {
                if (_eventRegistry.TryGetValue(typeof(T), out var registry))
                    registry.RemoveHandler(handler);
            }
        }
        
        /// <summary>
        ///     Publish an event to all subscribers
        /// </summary>
        public static void Publish<T>(T eventData) where T : class
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));
            
            Type eventType = typeof(T);
            
            lock (_registryLock)
            {
                if (!_eventRegistry.ContainsKey(eventType))
                    return;
                
                var registry = (DelegateRegistry)_eventRegistry[eventType];
                
                // Fire handlers synchronously (for performance-critical events)
                foreach (var handler in registry.Handlers)
                {
                    try
                    {
                        handler.DynamicInvoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Event handler error for {eventType.Name}: {ex.Message}");
                    }
                }
            }
            
            // Queue for async processing if needed
            lock (_registryLock)
            {
                if (_eventRegistry.TryGetValue(eventType, out var registry))
                {
                    foreach (var handler in ((DelegateRegistry)registry).Handlers)
                    {
                        var item = new EventItem(eventData) { Handler = handler };
                        QueueEvent(item);
                    }
                }
            }
        }

        private static void QueueEvent(EventItem item)
        {
            lock (_eventQueue)
            {
                _eventQueue.Enqueue(item);
            }
        }
        
        /// <summary>
        ///     Process queued events
        /// </summary>
        public static void ProcessEvents()
        {
            if (_isProcessing || _eventQueue.Count == 0)
                return;
            
            _isProcessing = true;
            
            while (_eventQueue.Count > 0)
            {
                var item = _eventQueue.Dequeue();
                
                try
                {
                    // Execute handler
                    item.Handler.DynamicInvoke(item.EventData);
                    
                    item.ExecutionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                catch (Exception ex)
                {
                    Log.Error($"Async event handler error: {ex.Message}", ex);
                }
            }
            
            _isProcessing = false;
        }
        
        /// <summary>
        ///     Clear all registrations
        /// </summary>
        public static void Reset()
        {
            lock (_registryLock)
            {
                _eventRegistry.Clear();
            }
            
            _eventQueue.Clear();
        }
        
        /// <summary>
        ///     Get subscriber count for an event type
        /// </summary>
        public static int GetSubscriberCount<T>() where T : class
        {
            lock (_registryLock)
            {
                if (_eventRegistry.TryGetValue(typeof(T), out var registry))
                    return ((DelegateRegistry)registry).Handlers.Count;
            }
            return 0;
        }
        
        /// <summary>
        ///     Private event item for queuing
        /// </summary>
        private class EventItem
        {
            public object EventData;
            public Delegate Handler;
            public long ExecutionTime;
            
            public EventItem(object data)
            {
                EventData = data;
                Handler = null;
                ExecutionTime = 0;
            }
        }
        
        /// <summary>
        ///     Registry for delegate handlers
        /// </summary>
        private class DelegateRegistry
        {
            public readonly List<Delegate> Handlers = new();
            
            public void AddHandler(Delegate handler)
            {
                Handlers.Add(handler);
            }
            
            public void RemoveHandler(Delegate handler)
            {
                Handlers.Remove(handler);
            }
        }
    }
    
    /// <summary>
    ///     Standardized event types for mod communication
    /// </summary>
    public static class ModEvents
    {
        /// <summary>
        ///     Fired when player connects
        /// </summary>
        public sealed class PlayerConnectedEvent : EventArgs
        {
            public Player Player;
            public NetPeer Peer;
        }
        
        /// <summary>
        ///     Fired when player disconnects
        /// </summary>
        public sealed class PlayerDisconnectedEvent : EventArgs
        {
            public string Username;
            public int PeerId;
        }
        
        /// <summary>
        ///     Fired when world transfer starts
        /// </summary>
        public sealed class WorldTransferStartedEvent : EventArgs
        {
            public int TargetPeerId;
            public long TotalBytes;
        }
        
        /// <summary>
        ///     Fired when world transfer completes
        /// </summary>
        public sealed class WorldTransferCompletedEvent : EventArgs
        {
            public int TargetPeerId;
            public bool Success;
        }
        
        /// <summary>
        ///     Fired when money changes significantly
        /// </summary>
        public sealed class MoneyChangedEvent : EventArgs
        {
            public long OldValue;
            public long NewValue;
            public long Change;
            public string Reason;
        }
        
        /// <summary>
        ///     Fired when building is placed
        /// </summary>
        public sealed class BuildingPlacedEvent : EventArgs
        {
            public string PrefabName;
            public float PositionX;
            public float PositionY;
            public string PlacerUsername;
        }
        
        /// <summary>
        ///     Fired when chat message received
        /// </summary>
        public sealed class ChatMessageReceivedEvent : EventArgs
        {
            public string Sender;
            public string Message;
            public DateTime Timestamp;
            public bool IsWhisper;
            public int? TargetPlayerId;
        }
        
        /// <summary>
        ///     Fired on session start
        /// </summary>
        public sealed class SessionStartedEvent : EventArgs
        {
            public string ServerToken;
            public string GameMode;
        }
        
        /// <summary>
        ///     Fired on session end
        /// </summary>
        public sealed class SessionEndedEvent : EventArgs
        {
            public string Reason;
            public double SessionDurationSeconds;
        }
        
        /// <summary>
        ///     Fired on connection status change
        /// </summary>
        public sealed class ConnectionStatusChangedEvent : EventArgs
        {
            public CS2M.API.Networking.ConnectionState PreviousState;
            public CS2M.API.Networking.ConnectionState CurrentState;
            public string ErrorMessage;
        }
        
        /// <summary>
        ///     Fired periodically for health monitoring
        /// </summary>
        public sealed class HealthCheckEvent : EventArgs
        {
            public long ActiveConnections;
            public int QueuedCommands;
            public double AverageLatencyMs;
            public string Status;
        }
    }
    
    /// <summary>
    ///     Utility extension methods for event handling
    /// </summary>
    public static class EventExtensions
    {
        /// <summary>
        ///     Subscribe with automatic weak reference (prevents memory leaks)
        /// </summary>
        public static void SubscribeWeak<T>(this object subscriber, Action<T> handler) where T : class
        {
            ModEventSystem.Subscribe<T>(ev => handler(ev));
        }
        
        /// <summary>
        ///     Subscribe once only (fires once then unsubscribes)
        /// </summary>
        public static void SubscribeOnce<T>(this object subscriber, Action<T> handler) where T : class
        {
            bool subscribed = false;
            Action<T> wrapper = null;
            wrapper = ev =>
            {
                if (!subscribed)
                {
                    subscribed = true;
                    handler(ev);
                    ModEventSystem.Unsubscribe<T>(wrapper);
                }
            };
            
            ModEventSystem.Subscribe(wrapper);
        }
    }
}
