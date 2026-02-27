// ============================================================================
// EventBus.cs
// Namespace: Condemned.Core
// Description: Static typed event bus for decoupled cross-system communication.
//              All inter-system events should go through here — never call into
//              another system's public methods directly.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Condemned.Core
{
    /// <summary>
    /// Lightweight static event bus. Supports any struct or class as an event type.
    /// 
    /// Usage:
    ///   // Subscribe
    ///   EventBus.Subscribe<GenCompletedEvent>(OnGenCompleted);
    ///
    ///   // Publish
    ///   EventBus.Publish(new GenCompletedEvent { GenIndex = 2 });
    ///
    ///   // Unsubscribe (always do this in OnDestroy)
    ///   EventBus.Unsubscribe<GenCompletedEvent>(OnGenCompleted);
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        /// <summary>Subscribe to an event type.</summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.ContainsKey(type))
                _handlers[type] = Delegate.Combine(_handlers[type], handler);
            else
                _handlers[type] = handler;
        }

        /// <summary>Unsubscribe from an event type. Call this in OnDestroy.</summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.ContainsKey(type))
            {
                var updated = Delegate.Remove(_handlers[type], handler);
                if (updated == null)
                    _handlers.Remove(type);
                else
                    _handlers[type] = updated;
            }
        }

        /// <summary>Publish an event to all subscribers.</summary>
        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handler))
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] Exception in handler for {type.Name}: {e}");
                }
            }
        }

        /// <summary>Clear all subscriptions. Call on scene unload.</summary>
        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
