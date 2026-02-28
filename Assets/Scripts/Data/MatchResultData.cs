// ============================================================================
// MatchResultData.cs
// Namespace: Condemned.Data
// Description: Immutable record of a completed match's outcome.
//              Created by MatchEndedEvent — passed to the EndScreen scene
//              via SceneLoader.SetMatchResult() for display and future
//              stat tracking.
// ============================================================================

using UnityEngine;
using Condemned.Core;

namespace Condemned.Data
{
    /// <summary>
    /// Snapshot of a match's final state. Serializable so it can be passed
    /// between scenes and stored locally.
    /// </summary>
    [System.Serializable]
    public class MatchResultData
    {
        // ─── Outcome ──────────────────────────────────────────────────────────

        public bool KillerWon;
        public int  SurvivorsEscaped;
        public int  SurvivorsSacrificed;

        // ─── Performance ──────────────────────────────────────────────────────

        public float MatchDurationSeconds;
        public int   GeneratorsCompleted;   // 0–5
        public int   TotalPalletsDropped;
        public int   TotalHooks;

        // ─── Derived ──────────────────────────────────────────────────────────

        public bool   SurvivorsWon      => !KillerWon;
        public string OutcomeLabel      => KillerWon ? "Killer Victory" : "Survivors Escaped";
        public string FormattedDuration => System.TimeSpan.FromSeconds(MatchDurationSeconds).ToString(@"mm\:ss");

        // ─── Factory ──────────────────────────────────────────────────────────

        /// <summary>
        /// Build a MatchResultData from a MatchEndedEvent and the GameStateManager.
        /// </summary>
        public static MatchResultData FromEvent(MatchEndedEvent e, float durationSeconds, int gensCompleted)
        {
            return new MatchResultData
            {
                KillerWon               = e.KillerWon,
                SurvivorsEscaped        = e.SurvivorsEscaped,
                SurvivorsSacrificed     = e.SurvivorsSacrificed,
                MatchDurationSeconds    = durationSeconds,
                GeneratorsCompleted     = gensCompleted,
            };
        }

        public override string ToString() =>
            $"[MatchResult] {OutcomeLabel} | " +
            $"Escaped={SurvivorsEscaped} Sacrificed={SurvivorsSacrificed} | " +
            $"Gens={GeneratorsCompleted}/5 | Duration={FormattedDuration}";
    }
}
