// ============================================================================
// HatchController.cs
// Namespace: Condemned.Gameplay
// Description: Hatch mechanic for the last surviving survivor.
//
//              Lifecycle:
//                Hidden   — hatch not yet relevant (>1 survivor alive)
//                Visible  — last survivor alive: hatch teleports to a random
//                           spawn point and becomes visible/interactable
//                Open     — survivor escaped through hatch
//                Closed   — killer closed the hatch; survivor must use exit gates
//
//              IInteractable: last survivor presses E to escape (instant).
//              IKillerInteractable: killer presses E to close it.
//
// Scene Setup:
//   1. Add to the Hatch prefab root GameObject
//   2. Populate _spawnPoints with empty child transforms at valid hatch
//      spawn locations around the map (recommend 6–10 points)
//   3. Assign _hatchMeshRoot (the visible mesh, hidden until activated)
// ============================================================================

using UnityEngine;
using Condemned.Core;
using Condemned.Systems;

namespace Condemned.Gameplay
{
    public enum HatchState { Hidden, Visible, Open, Closed }

    [AddComponentMenu("Condemned/Gameplay/Hatch Controller")]
    public class HatchController : MonoBehaviour, IInteractable, IKillerInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Spawn Points")]
        [Tooltip("Possible world positions where the hatch can appear. " +
                 "One is chosen randomly when the last survivor is alive.")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Visuals")]
        [Tooltip("Root of the hatch mesh hierarchy — hidden until hatch activates.")]
        [SerializeField] private GameObject  _hatchMeshRoot;

        [Header("Collider")]
        [Tooltip("The interaction collider — disabled while hatch is hidden.")]
        [SerializeField] private Collider    _interactCollider;

        // ─── State ────────────────────────────────────────────────────────────

        public HatchState State { get; private set; } = HatchState.Hidden;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            SetVisible(false);
            EventBus.Subscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
            EventBus.Subscribe<SurvivorEscapedEvent>(OnSurvivorEscaped);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
            EventBus.Unsubscribe<SurvivorEscapedEvent>(OnSurvivorEscaped);
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnSurvivorSacrificed(SurvivorSacrificedEvent e)
        {
            TryActivate();
        }

        private void OnSurvivorEscaped(SurvivorEscapedEvent e)
        {
            // If a survivor escaped through a gate, re-check hatch condition
            TryActivate();
        }

        private void TryActivate()
        {
            if (State != HatchState.Hidden) return;

            var gsm = GameStateManager.Instance;
            if (gsm == null || !gsm.IsLastSurvivor) return;

            Activate();
        }

        // ─── Activation ───────────────────────────────────────────────────────

        private void Activate()
        {
            State = HatchState.Visible;

            // Teleport to a random spawn point
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var pt = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
                transform.position = pt.position;
                transform.rotation = pt.rotation;
            }

            SetVisible(true);

            EventBus.Publish(new HatchSpawnedEvent { Position = transform.position });

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = 999f, // hatch creak is audible anywhere on the map
                Intensity = 1f,
                Type      = SoundType.Footstep,  // Placeholder — M8 adds HatchOpen sound type
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Hatch] Activated at {transform.position}.");
#endif
        }

        // ─── IInteractable — Survivor Escapes ─────────────────────────────────

        public void Interact(SurvivorController survivor)
        {
            if (State != HatchState.Visible) return;

            State = HatchState.Open;

            EventBus.Publish(new HatchOpenedEvent
            {
                SurvivorId = survivor.gameObject.GetInstanceID(),
            });

            EventBus.Publish(new SurvivorEscapedEvent
            {
                SurvivorId = survivor.gameObject.GetInstanceID(),
            });

            survivor.SetInputEnabled(false);
            survivor.gameObject.SetActive(false);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[Hatch] Last survivor escaped through the hatch!");
#endif

            SetVisible(false);
        }

        // ─── IKillerInteractable — Killer Closes ──────────────────────────────

        public void KillerInteract(KillerController killer)
        {
            if (State != HatchState.Visible) return;

            State = HatchState.Closed;
            SetVisible(false);

            EventBus.Publish(new HatchClosedEvent());

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[Hatch] Killer closed the hatch.");
#endif
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_hatchMeshRoot   != null) _hatchMeshRoot.SetActive(visible);
            if (_interactCollider != null) _interactCollider.enabled = visible;
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Spawn point indicators
            if (_spawnPoints != null)
            {
                Gizmos.color = new Color(0f, 0.8f, 0.8f, 0.5f);
                foreach (var pt in _spawnPoints)
                {
                    if (pt == null) continue;
                    Gizmos.DrawWireCube(pt.position, new Vector3(1f, 0.2f, 1f));
                    Gizmos.DrawLine(pt.position, pt.position + Vector3.up * 0.5f);
                }
            }

            // Current hatch state
            Gizmos.color = State switch
            {
                HatchState.Hidden  => Color.clear,
                HatchState.Visible => Color.green,
                HatchState.Open    => Color.cyan,
                HatchState.Closed  => Color.red,
                _                  => Color.white,
            };

            if (State != HatchState.Hidden)
            {
                Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.2f, 1f));
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.5f,
                    $"Hatch [{State}]");
            }
        }
#endif
    }
}
