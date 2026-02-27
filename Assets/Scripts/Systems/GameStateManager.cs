// ============================================================================
// GameStateManager.cs
// Namespace: Condemned.Systems
// Description: Host-authoritative match state machine. Single source of truth
//              for match progression. All systems read state from here;
//              all state changes go through here.
// ============================================================================

using System.Collections;
using UnityEngine;
using Condemned.Core;

namespace Condemned.Systems
{
    public class GameStateManager : Singleton<GameStateManager>
    {
        // ─── State ────────────────────────────────────────────────────────────

        public GameState CurrentState { get; private set; } = GameState.Idle;

        public int  TotalGenerators   { get; private set; } = 5;
        public int  GensCompleted     { get; private set; } = 0;
        public bool ExitGatesActive   => GensCompleted >= TotalGenerators;

        private int _survivorsAlive      = 4;
        private int _survivorsEscaped    = 0;
        private int _survivorsSacrificed = 0;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            EventBus.Subscribe<GenRepairedEvent>(OnGenRepaired);
            EventBus.Subscribe<SurvivorEscapedEvent>(OnSurvivorEscaped);
            EventBus.Subscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GenRepairedEvent>(OnGenRepaired);
            EventBus.Unsubscribe<SurvivorEscapedEvent>(OnSurvivorEscaped);
            EventBus.Unsubscribe<SurvivorSacrificedEvent>(OnSurvivorSacrificed);
        }

        // ─── State Transitions ────────────────────────────────────────────────

        /// <summary>Begin the warm-up phase (countdown before match starts).</summary>
        public void StartWarmUp()
        {
            TransitionTo(GameState.WarmUp);
        }

        /// <summary>Begin the active match.</summary>
        public void StartMatch()
        {
            _survivorsAlive      = 4;
            _survivorsEscaped    = 0;
            _survivorsSacrificed = 0;
            GensCompleted        = 0;

            TransitionTo(GameState.InProgress);
            EventBus.Publish(new MatchStartedEvent());
        }

        /// <summary>End the match.</summary>
        public void EndMatch(bool killerWon)
        {
            TransitionTo(GameState.Ended);
            EventBus.Publish(new MatchEndedEvent
            {
                KillerWon            = killerWon,
                SurvivorsEscaped     = _survivorsEscaped,
                SurvivorsSacrificed  = _survivorsSacrificed,
            });
        }

        private void TransitionTo(GameState next)
        {
            var previous = CurrentState;
            CurrentState = next;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] {previous} → {next}");
#endif
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnGenRepaired(GenRepairedEvent e)
        {
            GensCompleted = e.TotalCompleted;

            if (GensCompleted >= TotalGenerators)
            {
                EventBus.Publish(new ExitGatesActivatedEvent());
            }
        }

        private void OnSurvivorEscaped(SurvivorEscapedEvent e)
        {
            _survivorsEscaped++;
            _survivorsAlive--;
            CheckMatchEndCondition();
        }

        private void OnSurvivorSacrificed(SurvivorSacrificedEvent e)
        {
            _survivorsSacrificed++;
            _survivorsAlive--;
            CheckMatchEndCondition();
        }

        private void CheckMatchEndCondition()
        {
            if (CurrentState != GameState.InProgress) return;

            // All survivors resolved — killer wins if all sacrificed, survivors win if any escaped
            if (_survivorsAlive <= 0)
            {
                bool killerWon = _survivorsSacrificed > _survivorsEscaped;
                EndMatch(killerWon);
                return;
            }

            // Last survivor alive and gens incomplete: check hatch condition (handled by HatchManager)
        }

        // ─── Accessors ────────────────────────────────────────────────────────

        public bool IsMatchActive   => CurrentState == GameState.InProgress;
        public bool IsLastSurvivor  => _survivorsAlive == 1;
        public int  SurvivorsAlive  => _survivorsAlive;

#if UNITY_EDITOR
        // ── Editor debug shortcut ─────────────────────────────────────────────
        [ContextMenu("DEBUG: Force Start Match")]
        private void Debug_ForceStartMatch() => StartMatch();

        [ContextMenu("DEBUG: Force End Match (Killer Wins)")]
        private void Debug_ForceEndKillerWin() => EndMatch(true);

        [ContextMenu("DEBUG: Force End Match (Survivors Win)")]
        private void Debug_ForceEndSurvivorsWin() => EndMatch(false);
#endif
    }
}
