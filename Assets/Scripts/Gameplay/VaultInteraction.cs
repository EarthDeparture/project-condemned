// ============================================================================
// VaultInteraction.cs
// Namespace: Condemned.Gameplay
// Description: Window vault system. Survivors and the killer can vault through
//              windows. Direction is inferred from the actor's position relative
//              to the window centre — no separate trigger zones required.
//
//              Vault motion temporarily disables the CharacterController and
//              lerps the actor through the window. This is prototype-acceptable;
//              M6 will replace it with a matched Animator clip.
//
//              Killer vault is slower and permanently blocks the window for a
//              configurable duration, preventing further vaults.
//
// Scene Setup:
//   1. Add to the Window prefab root GameObject
//   2. Ensure a collider (non-trigger) exists on the window mesh for raycasts
//   3. Set _exitPointA and _exitPointB: two empty child GameObjects, one on
//      each side of the window at ground level
//   4. The window geometry must NOT be solid on the vault travel path —
//      disable the mesh collider or mark it as trigger during vault
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Vault Interaction")]
    public class VaultInteraction : MonoBehaviour, IInteractable, IKillerInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Vault Durations")]
        [Tooltip("Time (seconds) for a survivor to complete a vault.")]
        [SerializeField] private float _survivorVaultDuration = 0.85f;

        [Tooltip("Time (seconds) for the killer to complete a vault.")]
        [SerializeField] private float _killerVaultDuration   = 1.6f;

        [Header("Window Block")]
        [Tooltip("Seconds the window is blocked after the killer vaults through it.")]
        [SerializeField] private float _killerBlockDuration   = 15f;

        [Header("Sound")]
        [Tooltip("Sound event radius emitted when any actor vaults.")]
        [SerializeField] private float _vaultSoundRadius      = 18f;

        [Header("Exit Points")]
        [Tooltip("Empty child transform on one side of the window (ground level).")]
        [SerializeField] private Transform _exitPointA;

        [Tooltip("Empty child transform on the other side of the window (ground level).")]
        [SerializeField] private Transform _exitPointB;

        // ─── State ────────────────────────────────────────────────────────────

        private bool _isBlocked;
        private bool _isVaultInProgress;

        // ─── Properties ───────────────────────────────────────────────────────

        public bool IsBlocked => _isBlocked;

        // ─── IInteractable — Survivor ─────────────────────────────────────────

        public void Interact(SurvivorController survivor)
        {
            if (!CanVault()) return;

            Transform exit = GetExitPoint(survivor.transform.position);
            if (exit == null) return;

            StartCoroutine(C_SurvivorVault(survivor, exit));
        }

        // ─── IKillerInteractable — Killer ─────────────────────────────────────

        public void KillerInteract(KillerController killer)
        {
            if (!CanVault()) return;

            Transform exit = GetExitPoint(killer.transform.position);
            if (exit == null) return;

            StartCoroutine(C_KillerVault(killer, exit));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private bool CanVault() => !_isBlocked && !_isVaultInProgress;

        /// <summary>
        /// Determines which exit point to use.
        /// The actor exits on whichever side they are NOT currently on.
        /// We approximate this by picking the exit point that is FARTHER
        /// from the actor's current position.
        /// </summary>
        private Transform GetExitPoint(Vector3 actorPosition)
        {
            if (_exitPointA == null || _exitPointB == null)
            {
                Debug.LogError($"[VaultInteraction] Exit points not assigned on '{gameObject.name}'.");
                return null;
            }

            float distA = Vector3.Distance(actorPosition, _exitPointA.position);
            float distB = Vector3.Distance(actorPosition, _exitPointB.position);

            // Actor is on the same side as whichever point is CLOSER —
            // so the exit is the FARTHER point.
            return distA > distB ? _exitPointA : _exitPointB;
        }

        // ─── Vault Coroutines ─────────────────────────────────────────────────

        private IEnumerator C_SurvivorVault(SurvivorController survivor, Transform exitPoint)
        {
            _isVaultInProgress = true;
            survivor.SetInputEnabled(false);

            yield return C_MoveActor(
                survivor.transform,
                survivor.transform.position,
                exitPoint.position,
                _survivorVaultDuration);

            survivor.SetInputEnabled(true);

            EmitVaultSound(survivor.transform.position, isSurvivor: true);

            // TODO M6: trigger vault animation blend via Animator parameter

            _isVaultInProgress = false;
        }

        private IEnumerator C_KillerVault(KillerController killer, Transform exitPoint)
        {
            _isVaultInProgress = true;
            killer.SetInputEnabled(false);

            yield return C_MoveActor(
                killer.transform,
                killer.transform.position,
                exitPoint.position,
                _killerVaultDuration);

            killer.SetInputEnabled(true);

            EmitVaultSound(killer.transform.position, isSurvivor: false);

            // Block window after killer crosses it
            StartCoroutine(C_BlockWindow());

            _isVaultInProgress = false;
        }

        /// <summary>
        /// Lerps an actor's position from start to end over the given duration.
        /// Temporarily disables the CharacterController so the transform can be
        /// driven directly. Re-enables it on completion.
        /// </summary>
        private IEnumerator C_MoveActor(Transform actor, Vector3 from, Vector3 to, float duration)
        {
            var cc = actor.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                actor.position = Vector3.Lerp(from, to, t);
                yield return null;
            }

            actor.position = to;

            if (cc != null) cc.enabled = true;
        }

        private IEnumerator C_BlockWindow()
        {
            _isBlocked = true;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Vault] Window '{gameObject.name}' blocked for {_killerBlockDuration}s.");
#endif

            yield return new WaitForSeconds(_killerBlockDuration);

            _isBlocked = false;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Vault] Window '{gameObject.name}' unblocked.");
#endif
        }

        // ─── Sound ────────────────────────────────────────────────────────────

        private void EmitVaultSound(Vector3 position, bool isSurvivor)
        {
            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = position,
                Radius    = _vaultSoundRadius,
                Intensity = isSurvivor ? 0.45f : 0.6f,
                Type      = SoundType.Vault,
            });
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Exit points
            if (_exitPointA != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_exitPointA.position, 0.12f);
                Gizmos.DrawLine(transform.position, _exitPointA.position);
            }

            if (_exitPointB != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(_exitPointB.position, 0.12f);
                Gizmos.DrawLine(transform.position, _exitPointB.position);
            }

            // Blocked state indicator
            if (_isBlocked)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 2.0f, 0.3f));
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, "BLOCKED");
            }
        }
#endif
    }
}
