// ============================================================================
// Singleton.cs
// Namespace: Condemned.Core
// Description: Generic thread-safe singleton base class for manager objects.
// ============================================================================

using UnityEngine;

namespace Condemned.Core
{
    /// <summary>
    /// Generic singleton base. Inherit from this to create a persistent manager.
    /// Automatically handles DontDestroyOnLoad and duplicate instance prevention.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static T Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType<T>();

                        if (_instance == null)
                        {
                            var go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
            OnInitialize();
        }

        /// <summary>
        /// Called once after the singleton is initialized. Override instead of Awake.
        /// </summary>
        protected virtual void OnInitialize() { }
    }
}
