using System;
using System.Collections.Generic;

namespace CS2M.Util
{
    public static class ModEventSystem
    {
        private static readonly Dictionary<Type, List<Delegate>> _eventRegistry = new();
        private static readonly object _registryLock = new object();

        public static void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_registryLock)
            {
                if (!_eventRegistry.TryGetValue(typeof(T), out var handlers))
                {
                    handlers = new List<Delegate>();
                    _eventRegistry[typeof(T)] = handlers;
                }
                handlers.Add(handler);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_registryLock)
            {
                if (_eventRegistry.TryGetValue(typeof(T), out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public static void Publish<T>(T eventData) where T : class
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));

            Delegate[] snapshot;
            lock (_registryLock)
            {
                if (!_eventRegistry.TryGetValue(typeof(T), out var handlers) || handlers.Count == 0)
                    return;
                snapshot = handlers.ToArray();
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    handler.DynamicInvoke(eventData);
                }
                catch (Exception ex)
                {
                    Log.Error($"Event handler error for {typeof(T).Name}: {ex.Message}");
                }
            }
        }

        public static void Reset()
        {
            lock (_registryLock)
            {
                _eventRegistry.Clear();
            }
        }

        public static int GetSubscriberCount<T>() where T : class
        {
            lock (_registryLock)
            {
                if (_eventRegistry.TryGetValue(typeof(T), out var handlers))
                    return handlers.Count;
            }
            return 0;
        }
    }
}
