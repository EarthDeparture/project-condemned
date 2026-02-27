// ============================================================================
// SkillCheckManager.cs
// Namespace: Condemned.Systems
// Description: Decoupled skill check mini-game. Any system that needs a skill
//              check (generators, healing, hook-struggle) calls StartSkillCheck()
//              and receives a result callback. The visual rendering of the dial
//              is handled by the UI system in M9 — this class owns only the logic.
//
// Usage:
//   SkillCheckManager.Instance.StartSkillCheck(config, OnSuccess, OnFail);
//   SkillCheckManager.Instance.Submit(); // called when player presses space
// ============================================================================

using System;
using UnityEngine;
using Condemned.Core;

namespace Condemned.Systems
{
    // ─── Skill Check Config ───────────────────────────────────────────────────

    [Serializable]
    public class SkillCheckConfig
    {
        [Tooltip("How fast the dial rotates in degrees per second.")]
        [Range(80f, 400f)] public float DialSpeed   = 180f;

        [Tooltip("Size of the hit zone arc in degrees. Smaller = harder.")]
        [Range(10f, 60f)]  public float HitZoneSize  = 30f;

        [Tooltip("Size of the great zone as a fraction of the hit zone (0–1).")]
        [Range(0.1f, 0.5f)] public float GreatZoneFraction = 0.25f;

        [Tooltip("Progress awarded on a standard success (%).")]
        public float SuccessBonus = 3f;

        [Tooltip("Progress awarded on a great success (%).")]
        public float GreatBonus   = 5f;

        [Tooltip("Progress penalty on failure (%).")]
        public float FailPenalty  = 8f;

        public static SkillCheckConfig Default => new SkillCheckConfig();

        public static SkillCheckConfig Hard => new SkillCheckConfig
        {
            DialSpeed         = 280f,
            HitZoneSize       = 18f,
            GreatZoneFraction = 0.2f,
        };
    }

    // ─── Skill Check State (read by UI in M9) ─────────────────────────────────

    public class SkillCheckState
    {
        public bool  IsActive;
        public float DialAngle;       // 0–360, current dial position
        public float HitZoneStart;    // degrees
        public float HitZoneSize;     // degrees
        public float GreatZoneStart;  // degrees
        public float GreatZoneSize;   // degrees
    }

    // ─── Result ───────────────────────────────────────────────────────────────

    public enum SkillCheckResult { Success, GreatSuccess, Failure }

    // ─── Manager ──────────────────────────────────────────────────────────────

    public class SkillCheckManager : Singleton<SkillCheckManager>
    {
        // Current public state — polled by the UI system each frame in M9
        public SkillCheckState State { get; } = new SkillCheckState();

        private SkillCheckConfig _config;
        private Action<SkillCheckResult, float> _onComplete;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Begin a skill check.
        /// </summary>
        /// <param name="config">Difficulty configuration.</param>
        /// <param name="onComplete">
        ///   Callback receives (result, bonusOrPenalty).
        ///   bonusOrPenalty is positive on success, negative on failure.
        /// </param>
        public void StartSkillCheck(SkillCheckConfig config,
                                    Action<SkillCheckResult, float> onComplete)
        {
            if (State.IsActive)
            {
                Debug.LogWarning("[SkillCheckManager] Skill check already active — ignoring.");
                return;
            }

            _config    = config ?? SkillCheckConfig.Default;
            _onComplete = onComplete;

            // Randomise hit zone position on the dial
            State.HitZoneStart   = UnityEngine.Random.Range(0f, 360f);
            State.HitZoneSize    = _config.HitZoneSize;
            State.GreatZoneStart = State.HitZoneStart + _config.HitZoneSize * 0.5f
                                   - (_config.HitZoneSize * _config.GreatZoneFraction * 0.5f);
            State.GreatZoneSize  = _config.HitZoneSize * _config.GreatZoneFraction;
            State.DialAngle      = 0f;
            State.IsActive       = true;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[SkillCheck] Started — HitZone: {State.HitZoneStart:F0}°–" +
                      $"{State.HitZoneStart + State.HitZoneSize:F0}° | Speed: {_config.DialSpeed}°/s");
#endif
        }

        /// <summary>
        /// Submit the current dial position as a skill check attempt.
        /// Called by player input — bound to Space in the input system or
        /// forwarded from the active interactable's hold-press handler.
        /// </summary>
        public void Submit()
        {
            if (!State.IsActive) return;

            var result = EvaluateResult(State.DialAngle);
            float delta = result switch
            {
                SkillCheckResult.GreatSuccess => _config.GreatBonus,
                SkillCheckResult.Success      => _config.SuccessBonus,
                _                             => -_config.FailPenalty,
            };

            State.IsActive = false;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[SkillCheck] {result} at {State.DialAngle:F1}° → Δ{delta:+0.#;-0.#}%");
#endif
            _onComplete?.Invoke(result, delta);
            _onComplete = null;
        }

        /// <summary>Cancel an active skill check without a result (e.g. player stopped repairing).</summary>
        public void Cancel()
        {
            if (!State.IsActive) return;
            State.IsActive = false;
            _onComplete    = null;
        }

        // ─── Update ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (!State.IsActive) return;

            State.DialAngle = (State.DialAngle + _config.DialSpeed * Time.deltaTime) % 360f;
        }

        // ─── Evaluation ───────────────────────────────────────────────────────

        private SkillCheckResult EvaluateResult(float angle)
        {
            if (AngleInZone(angle, State.GreatZoneStart, State.GreatZoneSize))
                return SkillCheckResult.GreatSuccess;

            if (AngleInZone(angle, State.HitZoneStart, State.HitZoneSize))
                return SkillCheckResult.Success;

            return SkillCheckResult.Failure;
        }

        /// <summary>Check if an angle falls within a zone, handling 360° wrap-around.</summary>
        private static bool AngleInZone(float angle, float zoneStart, float zoneSize)
        {
            float end = (zoneStart + zoneSize) % 360f;
            return zoneStart <= end
                ? angle >= zoneStart && angle <= end
                : angle >= zoneStart || angle <= end; // wraps around 360
        }
    }
}
