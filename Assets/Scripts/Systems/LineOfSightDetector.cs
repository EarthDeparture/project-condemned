// ============================================================================
// LineOfSightDetector.cs
// Namespace: Condemned.Systems
// Description: Singleton that tracks killer-to-survivor line-of-sight using
//              physics raycasts. Runs at a fixed 10Hz update rate (configurable)
//              to avoid per-frame raycast overhead.
//
//              Output is a cached bool per survivor, queryable any time via
//              IsVisible() / IsVisibleById(). Locker-hidden survivors always
//              return false, bypassing the raycast entirely.
//
//              Consumed by:
//                - AI bot threat-awareness system (M3)
//                - HUD aura/red-stain indicator (M9)
//                - Any future perk that reacts to being seen/unseen
//
// Scene Setup:
//   1. Add to a persistent GameObject in the Bootstrap scene, or let the
//      Singleton auto-create it on first access
//   2. Assign _occlusionLayers to the LayerMask(s) that block vision
//      (e.g. "Wall", "Prop", "Terrain" — NOT the character layers)
//   3. Call SetKiller() when the killer spawns
//   4. Call RegisterSurvivor() / UnregisterSurvivor() as survivors spawn/die
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;

namespace Condemned.Systems
{
    [AddComponentMenu("Condemned/Systems/Line Of Sight Detector")]
    public class LineOfSightDetector : Singleton<LineOfSightDetector>
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Occlusion")]
        [Tooltip("LayerMask for geometry that blocks line-of-sight " +
                 "(walls, large props, terrain). Do NOT include character layers.")]
        [SerializeField] private LayerMask _occlusionLayers = ~0;

        [Header("Eye Height")]
        [Tooltip("Vertical offset (m) from each actor's pivot to approximate eye level.")]
        [SerializeField] private float _killerEyeHeight   = 1.7f;
        [SerializeField] private float _survivorEyeHeight = 1.6f;

        [Header("Update Rate")]
        [Tooltip("LOS checks per second. 10Hz is the spec; lower for performance if needed.")]
        [SerializeField, Range(1, 30)] private int _checksPerSecond = 10;

        // ─── References ───────────────────────────────────────────────────────

        private KillerController           _killer;
        private readonly List<SurvivorController> _survivors  = new();
        private readonly Dictionary<int, bool>    _visibility = new();

        // ─── Singleton Init ───────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            StartCoroutine(C_UpdateLoop());
        }

        // ─── Registration API ─────────────────────────────────────────────────

        /// <summary>
        /// Register the killer. Must be called when the killer spawns.
        /// Only one killer is supported (1v4 design).
        /// </summary>
        public void SetKiller(KillerController killer)
        {
            _killer = killer;
        }

        /// <summary>Register a survivor for LOS tracking.</summary>
        public void RegisterSurvivor(SurvivorController survivor)
        {
            if (survivor == null) return;

            if (!_survivors.Contains(survivor))
                _survivors.Add(survivor);

            _visibility[survivor.gameObject.GetInstanceID()] = false;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[LOS] Survivor registered. Tracking: {_survivors.Count}");
#endif
        }

        /// <summary>Unregister a survivor (on death, escape, or disconnect).</summary>
        public void UnregisterSurvivor(SurvivorController survivor)
        {
            if (survivor == null) return;

            _survivors.Remove(survivor);
            _visibility.Remove(survivor.gameObject.GetInstanceID());

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[LOS] Survivor unregistered. Tracking: {_survivors.Count}");
#endif
        }

        // ─── Query API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the killer has an unobstructed LOS to the given survivor.
        /// Locker-hidden survivors always return false.
        /// Returns false if the killer reference is null.
        /// </summary>
        public bool IsVisible(SurvivorController survivor)
        {
            if (survivor == null) return false;

            int id = survivor.gameObject.GetInstanceID();

            if (LockerInteraction.IsSurvivorHidden(id))
                return false;

            _visibility.TryGetValue(id, out bool visible);
            return visible;
        }

        /// <summary>
        /// Instance-ID overload — useful when you don't have a component reference.
        /// </summary>
        public bool IsVisibleById(int survivorInstanceId)
        {
            if (LockerInteraction.IsSurvivorHidden(survivorInstanceId))
                return false;

            _visibility.TryGetValue(survivorInstanceId, out bool visible);
            return visible;
        }

        // ─── Update Loop ──────────────────────────────────────────────────────

        private IEnumerator C_UpdateLoop()
        {
            var wait = new WaitForSeconds(1f / _checksPerSecond);

            while (true)
            {
                if (_killer != null)
                    RunVisibilityPass();

                yield return wait;
            }
        }

        private void RunVisibilityPass()
        {
            Vector3 killerEye = _killer.transform.position + Vector3.up * _killerEyeHeight;

            // Iterate in reverse so safe removal doesn't skip elements
            for (int i = _survivors.Count - 1; i >= 0; i--)
            {
                SurvivorController survivor = _survivors[i];

                // Clean up destroyed survivors automatically
                if (survivor == null)
                {
                    _survivors.RemoveAt(i);
                    continue;
                }

                int id = survivor.gameObject.GetInstanceID();

                // Locker-hidden: skip raycast entirely, mark as not visible
                if (LockerInteraction.IsSurvivorHidden(id))
                {
                    _visibility[id] = false;
                    continue;
                }

                Vector3 survivorEye = survivor.transform.position + Vector3.up * _survivorEyeHeight;
                Vector3 direction   = survivorEye - killerEye;
                float   distance    = direction.magnitude;

                // Raycast from killer eye toward survivor eye
                bool blocked = Physics.Raycast(
                    killerEye,
                    direction.normalized,
                    distance,
                    _occlusionLayers,
                    QueryTriggerInteraction.Ignore);

                _visibility[id] = !blocked;
            }
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_killer == null) return;

            Vector3 killerEye = _killer.transform.position + Vector3.up * _killerEyeHeight;

            foreach (var survivor in _survivors)
            {
                if (survivor == null) continue;

                int     id          = survivor.gameObject.GetInstanceID();
                Vector3 survivorEye = survivor.transform.position + Vector3.up * _survivorEyeHeight;

                _visibility.TryGetValue(id, out bool visible);
                bool hidden = LockerInteraction.IsSurvivorHidden(id);

                if (hidden)
                {
                    // Locker-hidden: dotted grey line
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    Gizmos.DrawLine(killerEye, survivorEye);
                }
                else if (visible)
                {
                    // Visible: solid red — killer can see this survivor
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(killerEye, survivorEye);
                    Gizmos.DrawWireSphere(survivorEye, 0.1f);
                }
                else
                {
                    // Occluded: dim grey
                    Gizmos.color = new Color(0.35f, 0.35f, 0.35f, 0.25f);
                    Gizmos.DrawLine(killerEye, survivorEye);
                }
            }
        }
#endif
    }
}
