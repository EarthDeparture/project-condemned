// ============================================================================
// ExitGateController.cs
// Namespace: Condemned.Gameplay
// Description: Exit gate state machine. Gates are locked until all 5
//              generators are repaired, then survivors can open them and
//              walk through to escape.
//
//              States:
//                Locked   — generators not done yet, cannot interact
//                Powered  — generators done, survivor can begin opening
//                Opening  — survivor channeling the open action (~20s)
//                Open     — gate is open; walk-through trigger fires escape
//
//              Implements IInteractable (survivor toggle-starts/cancels open).
//              Implements IKillerInteractable (killer cannot close an open gate
//              but can interrupt an in-progress open by downing the survivor).
//
// Scene Setup:
//   1. Add to the gate root GameObject
//   2. _escapeTrigger: a BoxCollider with IsTrigger=true at the gate opening
//      — survivors who enter this trigger while gate is Open will escape
//   3. _gateIndex: 0 or 1 (two gates per map)
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;
using Condemned.Systems;

namespace Condemned.Gameplay
{
    public enum ExitGateState { Locked, Powered, Opening, Open }

    [AddComponentMenu("Condemned/Gameplay/Exit Gate Controller")]
    public class ExitGateController : MonoBehaviour, IInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Identity")]
        [SerializeField] private int _gateIndex;

        [Header("Opening")]
        [Tooltip("Time (seconds) for a survivor to fully open the gate.")]
        [SerializeField] private float _openDuration  = 20f;

        [Tooltip("How close the survivor must stay during the opening channel (m).")]
        [SerializeField] private float _channelRange  = 2.5f;

        [Header("Escape Trigger")]
        [Tooltip("BoxCollider (IsTrigger=true) placed at the gate opening. " +
                 "Survivors entering this while gate is Open will escape.")]
        [SerializeField] private Collider _escapeTrigger;

        // ─── State ────────────────────────────────────────────────────────────

        public ExitGateState GateState { get; private set; } = ExitGateState.Locked;

        private SurvivorController _openingAgent;
        private Coroutine          _openCoroutine;
        private float              _openProgress; // 0–1

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            EventBus.Subscribe<ExitGatesActivatedEvent>(OnGatesActivated);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ExitGatesActivatedEvent>(OnGatesActivated);
        }

        // ─── Event Handler ────────────────────────────────────────────────────

        private void OnGatesActivated(ExitGatesActivatedEvent e)
        {
            if (GateState == ExitGateState.Locked)
            {
                GateState = ExitGateState.Powered;
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[ExitGate {_gateIndex}] Powered — survivors can now open.");
#endif
            }
        }

        // ─── IInteractable ────────────────────────────────────────────────────

        public void Interact(SurvivorController survivor)
        {
            switch (GateState)
            {
                case ExitGateState.Locked:
                    // Silent — gate is not interactive
                    break;

                case ExitGateState.Powered:
                    StartOpening(survivor);
                    break;

                case ExitGateState.Opening:
                    // Toggle: same survivor cancels, different survivor takes over
                    if (_openingAgent == survivor)
                        CancelOpening();
                    else
                        StartOpening(survivor);
                    break;

                case ExitGateState.Open:
                    // Gate already open — escape handled by trigger
                    break;
            }
        }

        // ─── Opening Channel ──────────────────────────────────────────────────

        private void StartOpening(SurvivorController survivor)
        {
            if (_openCoroutine != null)
                StopCoroutine(_openCoroutine);

            _openingAgent  = survivor;
            GateState      = ExitGateState.Opening;
            _openCoroutine = StartCoroutine(C_OpenChannel(survivor));

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[ExitGate {_gateIndex}] Opening started.");
#endif
        }

        private void CancelOpening()
        {
            if (_openCoroutine != null)
            {
                StopCoroutine(_openCoroutine);
                _openCoroutine = null;
            }

            GateState     = ExitGateState.Powered;
            _openingAgent = null;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[ExitGate {_gateIndex}] Opening cancelled.");
#endif
        }

        private IEnumerator C_OpenChannel(SurvivorController survivor)
        {
            float elapsed = 0f;

            while (elapsed < _openDuration)
            {
                elapsed += Time.deltaTime;
                _openProgress = elapsed / _openDuration;

                // Cancel if survivor wanders away
                float dist = Vector3.Distance(survivor.transform.position, transform.position);
                if (dist > _channelRange)
                {
                    CancelOpening();
                    yield break;
                }

                yield return null;
            }

            OpenGate();
        }

        private void OpenGate()
        {
            GateState      = ExitGateState.Open;
            _openingAgent  = null;
            _openProgress  = 1f;
            _openCoroutine = null;

            // Enable escape trigger collider
            if (_escapeTrigger != null)
                _escapeTrigger.enabled = true;

            EventBus.Publish(new ExitGateOpenedEvent { GateIndex = _gateIndex });

            EventBus.Publish(new SoundEmittedEvent
            {
                Position  = transform.position,
                Radius    = 40f,
                Intensity = 1f,
                Type      = SoundType.Footstep,  // Placeholder — M8 adds a distinct event type
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[ExitGate {_gateIndex}] OPEN.");
#endif
        }

        // ─── Escape Trigger ───────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (GateState != ExitGateState.Open) return;

            var survivor = other.GetComponentInParent<SurvivorController>();
            if (survivor == null) return;

            EventBus.Publish(new SurvivorEscapedEvent
            {
                SurvivorId = survivor.gameObject.GetInstanceID(),
            });

            survivor.SetInputEnabled(false);
            survivor.gameObject.SetActive(false);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[ExitGate {_gateIndex}] Survivor escaped!");
#endif
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var color = GateState switch
            {
                ExitGateState.Locked  => Color.gray,
                ExitGateState.Powered => Color.yellow,
                ExitGateState.Opening => Color.cyan,
                ExitGateState.Open    => Color.green,
                _                     => Color.white,
            };

            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f,
                new Vector3(3f, 4f, 0.3f));

            if (GateState == ExitGateState.Opening)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 4.5f,
                    $"Gate [{_gateIndex}] Opening... {_openProgress * 100f:F0}%");
            }
            else
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 4.5f,
                    $"Gate [{_gateIndex}] {GateState}");
            }
        }
#endif
    }
}
