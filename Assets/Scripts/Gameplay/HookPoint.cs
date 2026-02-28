// ============================================================================
// HookPoint.cs
// Namespace: Condemned.Gameplay
// Description: A hook anchor in the world. The killer carries a downed
//              survivor to a hook and interacts — the survivor is attached.
//
//              Hook Lifecycle:
//                Empty  ──→  Occupied (StageOne): survivor can be unhooked
//                            ↓  (no unhook in time)
//                            Occupied (StageTwo): death countdown begins
//                            ↓  (timer expires OR sacrifice)
//                            Broken: this hook is permanently unusable
//
//              IKillerInteractable: killer hooks carried survivor here.
//              IInteractable: nearby survivor unhooks the hooked survivor.
//
// Scene Setup:
//   1. Add to a hook anchor GameObject at appropriate height
//   2. Ensure a collider (non-trigger) exists for raycasts
//   3. HookSystem.Instance must exist in scene (auto-creates via Singleton)
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;
using Condemned.Systems;

namespace Condemned.Gameplay
{
    public enum HookOccupancyState { Empty, Occupied, Broken }

    [AddComponentMenu("Condemned/Gameplay/Hook Point")]
    public class HookPoint : MonoBehaviour, IKillerInteractable, IInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Hook Timing")]
        [Tooltip("Time (s) the survivor has to be rescued before entering StageTwo.")]
        [SerializeField] private float _stageOneDuration  = 60f;

        [Tooltip("Time (s) until sacrifice in StageTwo.")]
        [SerializeField] private float _stageTwoDuration  = 60f;

        [Tooltip("How long to wait after sacrifice before this hook becomes unusable.")]
        [SerializeField] private float _brokenDelay       = 0f;

        [Header("Unhook")]
        [Tooltip("Unhook interaction range — should match SurvivorController._interactionRange.")]
        [SerializeField] private float _unhookRange       = 2.5f;

        // ─── State ────────────────────────────────────────────────────────────

        public HookOccupancyState OccupancyState { get; private set; } = HookOccupancyState.Empty;
        public SurvivorController HookedSurvivor { get; private set; }
        public int                HookIndex      { get; set; }  // set by HookSystem at registration

        private float     _hookTimer;
        private int       _currentStage;  // 1 or 2
        private Coroutine _hookCoroutine;

        // ─── IKillerInteractable — Hook carried survivor ──────────────────────

        public void KillerInteract(KillerController killer)
        {
            if (OccupancyState != HookOccupancyState.Empty) return;

            SurvivorController carried = HookSystem.Instance?.GetCarried(killer);
            if (carried == null)
            {
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log("[HookPoint] Killer interacted but is not carrying a survivor.");
#endif
                return;
            }

            HookSurvivor(carried, killer);
        }

        // ─── IInteractable — Unhook ───────────────────────────────────────────

        public void Interact(SurvivorController rescuer)
        {
            if (OccupancyState != HookOccupancyState.Occupied) return;
            if (rescuer == HookedSurvivor)                      return; // can't unhook yourself

            Unhook(rescuer);
        }

        // ─── Hook ─────────────────────────────────────────────────────────────

        private void HookSurvivor(SurvivorController survivor, KillerController killer)
        {
            OccupancyState = HookOccupancyState.Occupied;
            HookedSurvivor = survivor;
            _currentStage  = 1;

            // Position the survivor on the hook
            survivor.transform.position = transform.position;
            survivor.SetInputEnabled(false);

            HookSystem.Instance?.ReleaseCarried(killer);

            EventBus.Publish(new SurvivorHookedEvent
            {
                SurvivorId = survivor.gameObject.GetInstanceID(),
                HookId     = GetInstanceID(),
                HookStage  = 1,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[HookPoint] Survivor hooked at stage 1. Timer: {_stageOneDuration}s");
#endif

            _hookCoroutine = StartCoroutine(C_HookProgress());
        }

        private IEnumerator C_HookProgress()
        {
            // ── Stage One ──────────────────────────────────────────────────────
            float elapsed = 0f;
            while (elapsed < _stageOneDuration && OccupancyState == HookOccupancyState.Occupied)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (OccupancyState != HookOccupancyState.Occupied) yield break;

            // Advance to Stage Two
            _currentStage = 2;
            int survivorId = HookedSurvivor.gameObject.GetInstanceID();

            EventBus.Publish(new SurvivorHookedEvent
            {
                SurvivorId = survivorId,
                HookId     = GetInstanceID(),
                HookStage  = 2,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[HookPoint] Survivor entered hook stage 2. Timer: {_stageTwoDuration}s");
#endif

            // ── Stage Two ──────────────────────────────────────────────────────
            elapsed = 0f;
            while (elapsed < _stageTwoDuration && OccupancyState == HookOccupancyState.Occupied)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (OccupancyState != HookOccupancyState.Occupied) yield break;

            // Sacrifice
            Sacrifice();
        }

        private void Unhook(SurvivorController rescuer)
        {
            if (_hookCoroutine != null)
            {
                StopCoroutine(_hookCoroutine);
                _hookCoroutine = null;
            }

            var unhooked = HookedSurvivor;
            OccupancyState = HookOccupancyState.Empty;
            HookedSurvivor = null;

            EventBus.Publish(new SurvivorUnhookedEvent
            {
                SurvivorId = unhooked.gameObject.GetInstanceID(),
                RescuerId  = rescuer.gameObject.GetInstanceID(),
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[HookPoint] Survivor unhooked.");
#endif
        }

        private void Sacrifice()
        {
            var sacrificed = HookedSurvivor;
            OccupancyState = HookOccupancyState.Broken;
            HookedSurvivor = null;
            _hookCoroutine = null;

            EventBus.Publish(new SurvivorSacrificedEvent
            {
                SurvivorId = sacrificed.gameObject.GetInstanceID(),
            });

            sacrificed.gameObject.SetActive(false);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log("[HookPoint] Survivor sacrificed. Hook broken.");
#endif
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            switch (OccupancyState)
            {
                case HookOccupancyState.Empty:
                    Gizmos.color = new Color(0.7f, 0.7f, 0f, 0.6f);
                    break;
                case HookOccupancyState.Occupied:
                    Gizmos.color = _currentStage == 2 ? Color.red : Color.yellow;
                    break;
                case HookOccupancyState.Broken:
                    Gizmos.color = Color.gray;
                    break;
            }

            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);

            if (OccupancyState == HookOccupancyState.Occupied)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
                Gizmos.DrawWireSphere(transform.position, _unhookRange);
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.0f,
                    $"Hook [{HookIndex}] Stage {_currentStage}\n{(int)(_hookTimer)}s");
            }
        }
#endif
    }
}
