// ============================================================================
// HookSystem.cs
// Namespace: Condemned.Systems
// Description: Singleton that manages the carry→hook flow.
//              Tracks which survivor each killer is currently carrying,
//              provides the nearest available HookPoint query, and
//              exposes the carry/release API consumed by KillerController.
//
// Usage:
//   HookSystem.Instance.CarrySurvivor(killer, survivor);
//   HookSystem.Instance.GetCarried(killer);      // → SurvivorController
//   HookSystem.Instance.ReleaseCarried(killer);
//   HookSystem.Instance.GetNearestHook(position); // → HookPoint
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;

namespace Condemned.Systems
{
    [AddComponentMenu("Condemned/Systems/Hook System")]
    public class HookSystem : Singleton<HookSystem>
    {
        // ─── State ────────────────────────────────────────────────────────────

        // Killer instance ID → carried survivor
        private readonly Dictionary<int, SurvivorController> _carried = new();

        // All hooks registered in the scene
        private readonly List<HookPoint> _hooks = new();

        // ─── Hook Registration ────────────────────────────────────────────────

        /// <summary>Register a hook that exists in the scene.</summary>
        public void RegisterHook(HookPoint hook)
        {
            if (hook == null) return;
            if (!_hooks.Contains(hook))
            {
                hook.HookIndex = _hooks.Count;
                _hooks.Add(hook);
            }
        }

        /// <summary>Unregister a hook (e.g. on scene cleanup).</summary>
        public void UnregisterHook(HookPoint hook)
        {
            _hooks.Remove(hook);
        }

        // ─── Carry API ────────────────────────────────────────────────────────

        /// <summary>
        /// Attach a downed survivor to the killer as "carried."
        /// Disables survivor input and positions them near the killer.
        /// Fires SurvivorCarriedEvent.
        /// </summary>
        public void CarrySurvivor(KillerController killer, SurvivorController survivor)
        {
            if (killer == null || survivor == null) return;

            int killerId = killer.gameObject.GetInstanceID();

            // Release any previously carried survivor (shouldn't happen, but safe)
            if (_carried.ContainsKey(killerId))
                ReleaseCarried(killer);

            _carried[killerId] = survivor;
            survivor.SetInputEnabled(false);

            EventBus.Publish(new SurvivorCarriedEvent
            {
                SurvivorId = survivor.gameObject.GetInstanceID(),
                KillerId   = killerId,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[HookSystem] Killer {killerId} now carrying survivor.");
#endif
        }

        /// <summary>Returns the survivor being carried by this killer, or null.</summary>
        public SurvivorController GetCarried(KillerController killer)
        {
            if (killer == null) return null;
            _carried.TryGetValue(killer.gameObject.GetInstanceID(), out var survivor);
            return survivor;
        }

        /// <summary>
        /// Release the killer's carried survivor without hooking.
        /// (Used when the survivor is placed on a hook by HookPoint.)
        /// </summary>
        public void ReleaseCarried(KillerController killer)
        {
            if (killer == null) return;
            _carried.Remove(killer.gameObject.GetInstanceID());
        }

        /// <summary>True if this killer is currently carrying a survivor.</summary>
        public bool IsCarrying(KillerController killer) =>
            killer != null && _carried.ContainsKey(killer.gameObject.GetInstanceID());

        // ─── Hook Query ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the nearest available (Empty, not Broken) hook to a world position.
        /// Returns null if no hooks are registered or all are occupied/broken.
        /// </summary>
        public HookPoint GetNearestHook(Vector3 position)
        {
            HookPoint nearest  = null;
            float     bestDist = float.MaxValue;

            foreach (var hook in _hooks)
            {
                if (hook == null)                                        continue;
                if (hook.OccupancyState != HookOccupancyState.Empty)   continue;

                float d = Vector3.Distance(position, hook.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest  = hook;
                }
            }

            return nearest;
        }

        /// <summary>All registered hooks (read-only view).</summary>
        public IReadOnlyList<HookPoint> Hooks => _hooks;

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Guard against dictionary being uninitialized during scene loading
            if (_carried == null) return;

            // Visualise carry chains
            foreach (var (killerId, survivor) in _carried)
            {
                if (survivor == null) continue;
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(survivor.transform.position,
                                survivor.transform.position + Vector3.up * 0.5f);
                UnityEditor.Handles.Label(
                    survivor.transform.position + Vector3.up * 0.8f, "CARRIED");
            }
        }
#endif
    }
}
