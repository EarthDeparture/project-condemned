// ============================================================================
// PalletInteraction.cs
// Namespace: Condemned.Gameplay
// Description: Pallet state machine — Standing → Dropped → Broken.
//
//              Standing:  No collision obstacle active. Survivor can drop it;
//                         killer passes through freely.
//              Dropped:   Obstacle collider enabled — blocks killer movement.
//                         Killer hit during drop → KillerStunnedEvent.
//                         Killer can break pallet over a timed channel.
//              Broken:    GameObject deactivated.
//
//              Implements IInteractable (survivor drops) and
//              IKillerInteractable (killer starts/cancels break channel).
//
// Scene Setup:
//   1. Add to the Pallet prefab root GameObject
//   2. Assign _obstacleCollider: the collider that becomes solid when dropped
//      (disabled by default in the prefab)
//   3. Assign _standingMesh / _droppedMesh (optional) for visual swap
//   4. Set _palletIndex uniquely if you need per-pallet event tracking
//
// Map Convention:
//   Pallet count per map should be enforced at map spawn level (20–25 total).
//   This component makes no assumption about total count — that is the
//   responsibility of the map/scene loader in M5.
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;

namespace Condemned.Gameplay
{
    // ─── State Enum ───────────────────────────────────────────────────────────

    public enum PalletState { Standing, Dropped, Broken }

    // ─── Component ────────────────────────────────────────────────────────────

    [AddComponentMenu("Condemned/Gameplay/Pallet Interaction")]
    public class PalletInteraction : MonoBehaviour, IInteractable, IKillerInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Identity")]
        [SerializeField] private int _palletIndex;

        [Header("Drop & Stun")]
        [Tooltip("Radius (m) within which the killer is stunned when the pallet drops on them.")]
        [SerializeField] private float _stunRadius      = 2.4f;

        [Tooltip("Duration (seconds) of the killer stun from a pallet drop.")]
        [SerializeField] private float _stunDuration    = 3.0f;

        [Header("Break Channel")]
        [Tooltip("Time (seconds) the killer must hold interaction to break the pallet.")]
        [SerializeField] private float _breakChannelTime = 3.0f;

        [Tooltip("How often (seconds) the break coroutine checks killer proximity.")]
        [SerializeField] private float _breakProximityCheckInterval = 0.25f;

        [Tooltip("Maximum distance (m) for the killer to maintain to continue breaking.")]
        [SerializeField] private float _breakMaxRange   = 2.5f;

        [Header("Collision")]
        [Tooltip("Collider that activates as a solid obstacle when the pallet is dropped.")]
        [SerializeField] private Collider _obstacleCollider;

        [Header("Visuals (optional)")]
        [Tooltip("Root of the standing pallet mesh — hidden when dropped.")]
        [SerializeField] private GameObject _standingMeshRoot;

        [Tooltip("Root of the dropped pallet mesh — shown when dropped.")]
        [SerializeField] private GameObject _droppedMeshRoot;

        // ─── State ────────────────────────────────────────────────────────────

        private PalletState      _state = PalletState.Standing;
        private KillerController _activeBreaker;
        private Coroutine        _breakCoroutine;

        // ─── Properties ───────────────────────────────────────────────────────

        public PalletState State      => _state;
        public bool        IsStanding => _state == PalletState.Standing;
        public bool        IsDropped  => _state == PalletState.Dropped;
        public bool        IsBroken   => _state == PalletState.Broken;

        // ─── Awake ────────────────────────────────────────────────────────────

        private void Awake()
        {
            ApplyStandingVisuals();

            if (_obstacleCollider != null)
                _obstacleCollider.enabled = false;
        }

        // ─── IInteractable — Survivor Drops ──────────────────────────────────

        public void Interact(SurvivorController survivor)
        {
            if (_state != PalletState.Standing) return;

            DropPallet();
        }

        // ─── IKillerInteractable — Killer Breaks ─────────────────────────────

        /// <summary>
        /// First call from a killer starts the break channel.
        /// A second call from the SAME killer cancels it (toggle semantics).
        /// Different killers replace the current breaker.
        /// </summary>
        public void KillerInteract(KillerController killer)
        {
            if (_state != PalletState.Dropped) return;

            if (_activeBreaker == killer)
            {
                CancelBreak();
                return;
            }

            // Cancel any previous channel and start a new one
            CancelBreak();
            _activeBreaker  = killer;
            _breakCoroutine = StartCoroutine(C_BreakChannel(killer));
        }

        // ─── Drop ─────────────────────────────────────────────────────────────

        private void DropPallet()
        {
            _state = PalletState.Dropped;

            // Enable physics obstacle
            if (_obstacleCollider != null)
                _obstacleCollider.enabled = true;

            ApplyDroppedVisuals();

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = 28f,
                Intensity = 0.75f,
                Type      = SoundType.PalletDrop,
            });

            CheckForKillerStun();

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Pallet {_palletIndex}] Dropped.");
#endif
        }

        private void CheckForKillerStun()
        {
            // OverlapSphere to find any killer within stun radius
            var colliders = Physics.OverlapSphere(transform.position, _stunRadius);

            foreach (var col in colliders)
            {
                var killer = col.GetComponentInParent<KillerController>();
                if (killer == null) continue;

                EventBus.Publish(new KillerStunnedEvent
                {
                    KillerId     = killer.gameObject.GetInstanceID(),
                    StunDuration = _stunDuration,
                });

#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[Pallet {_palletIndex}] Killer stunned ({_stunDuration}s).");
#endif
                // Only stun one killer per pallet drop (1v4 — there is only one)
                break;
            }
        }

        // ─── Break Channel ────────────────────────────────────────────────────

        private IEnumerator C_BreakChannel(KillerController killer)
        {
            float elapsed = 0f;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Pallet {_palletIndex}] Break channel started ({_breakChannelTime}s).");
#endif

            while (elapsed < _breakChannelTime)
            {
                yield return new WaitForSeconds(_breakProximityCheckInterval);
                elapsed += _breakProximityCheckInterval;

                // Abort if killer is gone or moved too far
                if (killer == null || !killer.gameObject.activeInHierarchy)
                {
                    CancelBreak();
                    yield break;
                }

                float dist = Vector3.Distance(killer.transform.position, transform.position);
                if (dist > _breakMaxRange)
                {
                    CancelBreak();
                    yield break;
                }
            }

            BreakPallet();
        }

        private void CancelBreak()
        {
            if (_breakCoroutine != null)
            {
                StopCoroutine(_breakCoroutine);
                _breakCoroutine = null;
            }

            _activeBreaker = null;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Pallet {_palletIndex}] Break cancelled.");
#endif
        }

        private void BreakPallet()
        {
            _state         = PalletState.Broken;
            _activeBreaker = null;
            _breakCoroutine = null;

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = 30f,
                Intensity = 0.85f,
                Type      = SoundType.PalletDrop,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Pallet {_palletIndex}] Broken and removed.");
#endif

            // TODO M6: play break animation clip before deactivating
            gameObject.SetActive(false);
        }

        // ─── Visuals ──────────────────────────────────────────────────────────

        private void ApplyStandingVisuals()
        {
            if (_standingMeshRoot != null) _standingMeshRoot.SetActive(true);
            if (_droppedMeshRoot  != null) _droppedMeshRoot.SetActive(false);
        }

        private void ApplyDroppedVisuals()
        {
            if (_standingMeshRoot != null) _standingMeshRoot.SetActive(false);
            if (_droppedMeshRoot  != null) _droppedMeshRoot.SetActive(true);
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            switch (_state)
            {
                case PalletState.Standing:
                    Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f); // amber
                    Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f,
                        new Vector3(0.25f, 1.8f, 1.6f));
                    break;

                case PalletState.Dropped:
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f,
                        new Vector3(0.25f, 0.2f, 1.6f));

                    // Stun radius
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.08f);
                    Gizmos.DrawWireSphere(transform.position, _stunRadius);

                    // Break progress label
                    if (_activeBreaker != null)
                    {
                        UnityEditor.Handles.Label(
                            transform.position + Vector3.up * 1.0f,
                            "Breaking...");
                    }
                    break;

                case PalletState.Broken:
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(transform.position,
                        new Vector3(0.1f, 0.05f, 1.6f));
                    break;
            }

            // Index label
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.0f,
                $"Pallet [{_palletIndex}] {_state}");
        }
#endif
    }
}
