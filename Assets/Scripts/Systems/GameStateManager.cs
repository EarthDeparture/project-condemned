// ============================================================================
// GameStateManager.cs
// Namespace: Condemned.Systems
// Description: Host-authoritative match state machine. Single source of truth
//              for match progression. All systems read state from here;
//              all state changes go through here.
//
// Bug fix (M2): OnGenRepaired increments own counter (e.TotalCompleted = -1 sentinel).
// ============================================================================

using UnityEngine;
using Condemned.Core;

namespace Condemned.Systems
{
    public class GameStateManager : Singleton<GameStateManager>
    {
        // ─── Configuration ────────────────────────────────────────────────────

        [Header("Match Configuration")]
        [Tooltip("Number of generators required to activate exit gates.")]
        [SerializeField] private int _totalGenerators = 5;

        // ─── State ────────────────────────────────────────────────────────────

        public GameState CurrentState    { get; private set; } = GameState.Idle;
        public int  TotalGenerators      => _totalGenerators;
        public int  GensCompleted        { get; private set; }
        public bool ExitGatesActive      => GensCompleted >= _totalGenerators;
        public bool IsMatchActive        => CurrentState == GameState.InProgress;
        public bool IsLastSurvivor       => _survivorsAlive == 1;
        public int  SurvivorsAlive       => _survivorsAlive;
        public int  SurvivorsEscaped     => _survivorsEscaped;
        public int  SurvivorsSacrificed  => _survivorsSacrificed;

        private int   _survivorsAlive      = 4;
        private int   _survivorsEscaped    = 0;
        private int   _survivorsSacrificed = 0;
        private float _matchStartTime;

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

        public void StartWarmUp() => TransitionTo(GameState.WarmUp);

        public void StartMatch(int survivorCount = 4)
        {
            _survivorsAlive      = survivorCount;
            _survivorsEscaped    = 0;
            _survivorsSacrificed = 0;
            GensCompleted        = 0;
            _matchStartTime      = Time.time;

            TransitionTo(GameState.InProgress);
            EventBus.Publish(new MatchStartedEvent());

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] Match started with {survivorCount} survivors, {_totalGenerators} generators.");
#endif
        }

        public void EndMatch(bool killerWon)
        {
            if (CurrentState == GameState.Ended) return;

            float duration = Time.time - _matchStartTime;

            TransitionTo(GameState.Ended);
            EventBus.Publish(new MatchEndedEvent
            {
                KillerWon           = killerWon,
                SurvivorsEscaped    = _survivorsEscaped,
                SurvivorsSacrificed = _survivorsSacrificed,
            });

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] Match ended. KillerWon={killerWon}, " +
                      $"Escaped={_survivorsEscaped}, Sacrificed={_survivorsSacrificed}, " +
                      $"Duration={duration:F1}s");
#endif
        }

        private void TransitionTo(GameState next)
        {
            var prev = CurrentState;
            CurrentState = next;
#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] {prev} → {next}");
#endif
        }

        // ─── Event Handlers ───────────────────────────────────────────────────

        private void OnGenRepaired(GenRepairedEvent e)
        {
            // FIX: increment own counter — do NOT trust e.TotalCompleted
            // GeneratorInteraction passes -1 as a sentinel; the manager owns this count.
            GensCompleted++;

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] Generator #{e.GenIndex} repaired. " +
                      $"Progress: {GensCompleted}/{_totalGenerators}");
#endif

            if (GensCompleted >= _totalGenerators)
            {
#if DEBUG || DEVELOPMENT_BUILD
                Debug.Log("[GameStateManager] All generators repaired — exit gates activated!");
#endif
                EventBus.Publish(new ExitGatesActivatedEvent());
            }
        }

        private void OnSurvivorEscaped(SurvivorEscapedEvent e)
        {
            _survivorsEscaped++;
            _survivorsAlive = Mathf.Max(0, _survivorsAlive - 1);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] Survivor escaped. Alive: {_survivorsAlive}");
#endif

            CheckMatchEndCondition();
        }

        private void OnSurvivorSacrificed(SurvivorSacrificedEvent e)
        {
            _survivorsSacrificed++;
            _survivorsAlive = Mathf.Max(0, _survivorsAlive - 1);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[GameStateManager] Survivor sacrificed. Alive: {_survivorsAlive}");
#endif

            CheckMatchEndCondition();
        }

        private void CheckMatchEndCondition()
        {
            if (CurrentState != GameState.InProgress) return;

            if (_survivorsAlive <= 0)
            {
                bool killerWon = _survivorsEscaped == 0;
                EndMatch(killerWon);
            }
            // Note: last-survivor hatch condition is managed by HatchController,
            // which subscribes to SurvivorSacrificedEvent and checks IsLastSurvivor.
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Elapsed match time in seconds.</summary>
        public float MatchDuration => IsMatchActive ? Time.time - _matchStartTime : 0f;

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("DEBUG: Force Start Match")]
        private void Debug_ForceStartMatch() => StartMatch();

        [ContextMenu("DEBUG: Force End Match (Killer Wins)")]
        private void Debug_ForceEndKillerWin() => EndMatch(true);

        [ContextMenu("DEBUG: Force End Match (Survivors Win)")]
        private void Debug_ForceEndSurvivorsWin() => EndMatch(false);

        [ContextMenu("DEBUG: Simulate Gen Repaired")]
        private void Debug_SimulateGenRepaired() =>
            EventBus.Publish(new GenRepairedEvent { GenIndex = GensCompleted, TotalCompleted = -1 });
#endif
    }
}
