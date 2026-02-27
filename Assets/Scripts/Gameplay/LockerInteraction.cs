// ============================================================================
// LockerInteraction.cs
// Namespace: Condemned.Gameplay
// Description: Locker hide/exit mechanic.
//
//              A survivor presses E to enter a locker. While inside:
//                - Input is disabled (survivor cannot move)
//                - They are hidden from the killer's LOS
//                - A locker creak sound is emitted (audible to killer)
//
//              The same survivor presses E again to exit (tap to exit).
//              The killer can pull the survivor by holding E near the locker,
//              which forces an exit and fires a SurvivorHitEvent (Injured state).
//
//              Hidden state is exposed via IsSurvivorHidden(int instanceId) —
//              a static query consumed by LineOfSightDetector.
//
// Scene Setup:
//   1. Add to the Locker prefab root GameObject
//   2. Ensure a collider (non-trigger) exists for survivor interaction raycasts
//   3. Assign _exitOffset: offset from locker pivot where survivor is placed on exit
//      (usually transform.forward * 0.8f in the prefab)
//   4. Only one survivor fits per locker
//
// TODO M3: Block entry when survivor is in Injured/Dying HitState
//          (requires SurvivorController.IsInjured public getter or a health-
//          state query from the yet-to-be-built HealthSystem).
// ============================================================================

using UnityEngine;
using Condemned.Core;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Locker Interaction")]
    public class LockerInteraction : MonoBehaviour, IInteractable, IKillerInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Exit Position")]
        [Tooltip("Local-space offset applied to the locker's position when the " +
                 "survivor exits. Default: forward 0.8m.")]
        [SerializeField] private Vector3 _exitOffset = new Vector3(0f, 0f, 0.8f);

        [Header("Sound")]
        [Tooltip("Radius (m) of the creak sound emitted on enter and exit.")]
        [SerializeField] private float _creakSoundRadius = 22f;

        [Tooltip("Intensity (0–1) of the creak sound event.")]
        [SerializeField, Range(0f, 1f)] private float _creakIntensity = 0.5f;

        [Header("Killer Pull")]
        [Tooltip("Hit state the survivor is set to when the killer pulls them out.")]
        [SerializeField] private HitState _killerPullResultState = HitState.Injured;

        // ─── Static Hidden-Survivor Registry ──────────────────────────────────
        // Queried by LineOfSightDetector without requiring a component reference.

        private static readonly System.Collections.Generic.HashSet<int> _hiddenIds
            = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Returns true if the survivor (by GameObject.GetInstanceID()) is
        /// currently hiding inside any locker in the scene.
        /// </summary>
        public static bool IsSurvivorHidden(int survivorInstanceId)
            => _hiddenIds.Contains(survivorInstanceId);

        // ─── State ────────────────────────────────────────────────────────────

        private SurvivorController _occupant;

        /// <summary>True if a survivor is currently inside this locker.</summary>
        public bool IsOccupied => _occupant != null;

        // ─── IInteractable — Survivor Enter / Exit ────────────────────────────

        public void Interact(SurvivorController survivor)
        {
            if (_occupant == null)
            {
                // Locker is empty — attempt entry
                EnterLocker(survivor);
            }
            else if (_occupant == survivor)
            {
                // Same survivor pressing E again — exit
                ExitLocker(survivor);
            }
            // Else: occupied by a different survivor — silently ignore
        }

        // ─── IKillerInteractable — Killer Pull ────────────────────────────────

        public void KillerInteract(KillerController killer)
        {
            if (_occupant == null) return;

            var victim = _occupant;

            // Force exit first, then apply hit state
            ForceExit(victim);

            EventBus.Publish(new SurvivorHitEvent
            {
                SurvivorId  = victim.gameObject.GetInstanceID(),
                NewState    = _killerPullResultState,
                HitPosition = transform.position,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Locker] Killer pulled survivor — hit state: {_killerPullResultState}.");
#endif
        }

        // ─── Enter ────────────────────────────────────────────────────────────

        private void EnterLocker(SurvivorController survivor)
        {
            // TODO M3: reject entry if survivor is Injured/Dying
            // Requires SurvivorController.IsInjured (public getter) or HealthSystem.

            _occupant = survivor;

            int id = survivor.gameObject.GetInstanceID();
            _hiddenIds.Add(id);

            // Place survivor inside the locker and disable their input
            survivor.transform.position = transform.position;
            survivor.SetInputEnabled(false);

            EmitCreakSound();

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Locker] Survivor entered. Hidden IDs: {_hiddenIds.Count}");
#endif
        }

        // ─── Exit ─────────────────────────────────────────────────────────────

        private void ExitLocker(SurvivorController survivor)
        {
            ForceExit(survivor);
            EmitCreakSound();

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[Locker] Survivor exited.");
#endif
        }

        /// <summary>
        /// Unconditionally removes the occupant, re-enables their input,
        /// and places them at the exit position. Does NOT emit sound —
        /// callers are responsible for that where appropriate.
        /// </summary>
        private void ForceExit(SurvivorController survivor)
        {
            if (survivor == null) return;

            int id = survivor.gameObject.GetInstanceID();
            _hiddenIds.Remove(id);

            _occupant = null;

            // Place survivor at exit offset (world space)
            survivor.transform.position = transform.TransformPoint(_exitOffset);
            survivor.SetInputEnabled(true);
        }

        // ─── Sound ────────────────────────────────────────────────────────────

        private void EmitCreakSound()
        {
            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = _creakSoundRadius,
                Intensity = _creakIntensity,
                Type      = SoundType.LockerUse,
            });
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            // Ensure stale hidden IDs don't persist if the locker is destroyed
            if (_occupant != null)
            {
                _hiddenIds.Remove(_occupant.gameObject.GetInstanceID());
            }
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Locker body outline
            Gizmos.color = IsOccupied
                ? Color.magenta
                : new Color(0.5f, 0f, 0.8f, 0.5f);

            Gizmos.DrawWireCube(
                transform.position + Vector3.up * 0.95f,
                new Vector3(0.6f, 1.9f, 0.6f));

            // Exit point indicator
            Vector3 exitWorldPos = transform.TransformPoint(_exitOffset);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, exitWorldPos);
            Gizmos.DrawSphere(exitWorldPos, 0.08f);

            // Sound radius
            Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.04f);
            Gizmos.DrawWireSphere(transform.position, _creakSoundRadius);

            // Occupant label
            if (IsOccupied)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.3f,
                    "OCCUPIED");
            }
        }
#endif
    }
}
