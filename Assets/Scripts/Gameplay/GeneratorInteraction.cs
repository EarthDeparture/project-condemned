// ============================================================================
// GeneratorInteraction.cs
// Namespace: Condemned.Gameplay
// Description: Generator state machine. Tracks repair progress (0–100%),
//              manages multiple simultaneous repairers, fires random skill
//              checks, handles passive regression when abandoned, and processes
//              killer kicks (forced regression burst).
//
//              Implements IInteractable for survivors (toggle start/stop repair)
//              and IKillerInteractable for the killer (kick = instant regression
//              burst + brief forced regression window).
//
// Scene Setup:
//   1. Add to the Generator prefab root GameObject
//   2. Set _genIndex uniquely per generator (0–6 for 7-gen maps)
//   3. Ensure a collider (non-trigger) exists for survivor interaction raycasts
//   4. Assign _interactPromptAnchor (optional UI world-space anchor)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Condemned.Core;
using Condemned.Systems;

namespace Condemned.Gameplay
{
    [AddComponentMenu("Condemned/Gameplay/Generator Interaction")]
    public class GeneratorInteraction : MonoBehaviour, IInteractable, IKillerInteractable
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Unique index for this generator (0–6 on a standard map).")]
        [SerializeField] private int _genIndex;

        [Header("Repair")]
        [Tooltip("Repair rate in percent per second, per active repairer.")]
        [SerializeField] private float _repairRatePerSurvivor = 5f;

        [Header("Regression")]
        [Tooltip("Passive regression rate (% per second) when the gen is abandoned mid-repair.")]
        [SerializeField] private float _passiveRegressionRate = 2f;
        [Tooltip("Whether passive regression is active (disabled on some perks/game modes).")]
        [SerializeField] private bool  _passiveRegressionEnabled = true;

        [Header("Skill Check")]
        [Tooltip("Minimum repair time (seconds) before the first skill check fires.")]
        [SerializeField] private float _skillCheckIntervalMin = 10f;
        [Tooltip("Maximum repair time (seconds) before the first skill check fires.")]
        [SerializeField] private float _skillCheckIntervalMax = 20f;

        [Header("Killer Kick")]
        [Tooltip("Instant progress penalty applied the moment the killer kicks (%).")]
        [SerializeField] private float _kickInstantPenalty     = 15f;
        [Tooltip("Duration of the forced regression window after a kick (seconds).")]
        [SerializeField] private float _kickForcedRegressionTime = 5f;
        [Tooltip("Regression rate during the forced regression window (% per second).")]
        [SerializeField] private float _kickForcedRegressionRate = 4f;

        // ─── State ────────────────────────────────────────────────────────────

        private float _progress;          // 0–100
        private bool  _isRepaired;
        private bool  _isRegressing;
        private bool  _isForcedRegressing;

        // Active repairers — survivors call Interact() to toggle on/off
        private readonly HashSet<SurvivorController> _repairers = new();

        // Skill check scheduling
        private float _nextSkillCheckTime;

        // ─── Properties ───────────────────────────────────────────────────────

        public float Progress      => _progress;
        public bool  IsRepaired    => _isRepaired;
        public int   RepairerCount => _repairers.Count;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            ScheduleNextSkillCheck();
        }

        private void Update()
        {
            if (_isRepaired) return;

            if (_repairers.Count > 0)
            {
                _isRegressing = false;
                TickRepair();
                MaybeFireSkillCheck();
            }
            else if (_isForcedRegressing || _passiveRegressionEnabled)
            {
                TickPassiveRegression();
            }
        }

        // ─── IInteractable — Survivor Toggle ─────────────────────────────────

        /// <summary>
        /// Called by SurvivorController.TryInteract() on interaction press.
        /// First call starts repair; second call stops (toggle semantics).
        /// </summary>
        public void Interact(SurvivorController survivor)
        {
            if (_isRepaired) return;

            if (_repairers.Contains(survivor))
                StopRepairing(survivor);
            else
                StartRepairing(survivor);
        }

        // ─── IKillerInteractable — Kick ───────────────────────────────────────

        /// <summary>
        /// Called by KillerController.TryInteract().
        /// Ejects all repairers, applies instant progress penalty, begins
        /// forced regression for _kickForcedRegressionTime seconds.
        /// </summary>
        public void KillerInteract(KillerController killer)
        {
            if (_isRepaired || _isForcedRegressing) return;

            // Eject all active repairers
            var snapshot = new List<SurvivorController>(_repairers);
            foreach (var s in snapshot) StopRepairing(s);

            // Instant progress penalty
            _progress = Mathf.Max(0f, _progress - _kickInstantPenalty);

            // Cancel any active skill check
            SkillCheckManager.Instance?.Cancel();

            // Begin forced regression window
            StartCoroutine(C_ForcedRegression());

            EventBus.Publish(new GenKickedEvent
            {
                GenIndex = _genIndex,
                Position = transform.position,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Generator {_genIndex}] Kicked by killer. Progress → {_progress:F1}%");
#endif
        }

        // ─── Repair ───────────────────────────────────────────────────────────

        private void StartRepairing(SurvivorController survivor)
        {
            _repairers.Add(survivor);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Generator {_genIndex}] Repairer added. Active: {_repairers.Count}");
#endif
        }

        private void StopRepairing(SurvivorController survivor)
        {
            _repairers.Remove(survivor);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Generator {_genIndex}] Repairer removed. Active: {_repairers.Count}");
#endif
        }

        private void TickRepair()
        {
            float delta = _repairRatePerSurvivor * _repairers.Count * Time.deltaTime;
            _progress = Mathf.Min(100f, _progress + delta);

            EventBus.Publish(new GenProgressChangedEvent
            {
                GenIndex = _genIndex,
                Progress = _progress,
            });

            if (_progress >= 100f)
                CompleteRepair();
        }

        private void CompleteRepair()
        {
            if (_isRepaired) return;

            _isRepaired = true;

            // Eject remaining repairers
            var snapshot = new List<SurvivorController>(_repairers);
            foreach (var s in snapshot) StopRepairing(s);

            SkillCheckManager.Instance?.Cancel();

            // TotalCompleted tracking is owned by the match manager (M5).
            // Pass -1 as a sentinel so the match manager does its own count.
            EventBus.Publish(new GenRepairedEvent
            {
                GenIndex       = _genIndex,
                TotalCompleted = -1,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Generator {_genIndex}] ✓ Repaired!");
#endif
        }

        // ─── Regression ───────────────────────────────────────────────────────

        private void TickPassiveRegression()
        {
            if (_isForcedRegressing) return; // handled by coroutine

            if (_progress <= 0f) return;

            if (!_isRegressing)
            {
                _isRegressing = true;
                EventBus.Publish(new GenRegressionStartedEvent { GenIndex = _genIndex });
            }

            _progress = Mathf.Max(0f, _progress - _passiveRegressionRate * Time.deltaTime);

            EventBus.Publish(new GenProgressChangedEvent
            {
                GenIndex = _genIndex,
                Progress = _progress,
            });
        }

        private IEnumerator C_ForcedRegression()
        {
            _isForcedRegressing = true;
            _isRegressing       = true;

            EventBus.Publish(new GenRegressionStartedEvent { GenIndex = _genIndex });

            float elapsed = 0f;

            while (elapsed < _kickForcedRegressionTime && !_isRepaired)
            {
                _progress = Mathf.Max(0f, _progress - _kickForcedRegressionRate * Time.deltaTime);

                EventBus.Publish(new GenProgressChangedEvent
                {
                    GenIndex = _genIndex,
                    Progress = _progress,
                });

                elapsed += Time.deltaTime;
                yield return null;
            }

            _isForcedRegressing = false;
            _isRegressing       = false;
        }

        // ─── Skill Check ──────────────────────────────────────────────────────

        private void ScheduleNextSkillCheck()
        {
            _nextSkillCheckTime = Time.time + Random.Range(_skillCheckIntervalMin, _skillCheckIntervalMax);
        }

        private void MaybeFireSkillCheck()
        {
            if (SkillCheckManager.Instance == null)        return;
            if (SkillCheckManager.Instance.State.IsActive) return;
            if (Time.time < _nextSkillCheckTime)           return;

            ScheduleNextSkillCheck();

            SkillCheckManager.Instance.StartSkillCheck(
                SkillCheckConfig.Default,
                OnSkillCheckResult);
        }

        private void OnSkillCheckResult(SkillCheckResult result, float delta)
        {
            _progress = Mathf.Clamp(_progress + delta, 0f, 100f);

            EventBus.Publish(new GenProgressChangedEvent
            {
                GenIndex = _genIndex,
                Progress = _progress,
            });

            if (result == SkillCheckResult.Failure)
            {
                // M9 will wire audio (explosion spark sound) and camera shake here
                // via an additional event. For now the GenProgressChangedEvent suffices.
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log($"[Generator {_genIndex}] Skill check failed — progress −{-delta:F1}%");
#endif
            }

            if (_progress >= 100f)
                CompleteRepair();
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isRepaired
                ? Color.green
                : _isForcedRegressing
                    ? Color.red
                    : new Color(1f, 0.55f, 0f);

            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.9f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.3f,
                $"Gen [{_genIndex}]  {_progress:F0}%  " +
                $"({_repairers.Count} repairing)");
        }
#endif
    }
}
