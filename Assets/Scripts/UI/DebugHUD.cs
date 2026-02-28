// ============================================================================
// DebugHUD.cs
// Namespace: Condemned.UI
// Description: Development-only on-screen overlay. Displays match state,
//              generator progress, survivor health, kill/escape counts,
//              and LOS status. Rendered via OnGUI — zero dependencies on
//              UI prefabs or Canvas setup.
//
//              Automatically disabled in release builds.
//              Toggle with F1 at runtime.
// ============================================================================

using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;
using Condemned.Systems;

namespace Condemned.UI
{
    [AddComponentMenu("Condemned/UI/Debug HUD")]
    public class DebugHUD : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Display")]
        [SerializeField] private KeyCode  _toggleKey    = KeyCode.F1;
        [SerializeField] private int      _fontSize      = 16;
        [SerializeField] private Color    _bgColor       = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private bool     _showOnStart   = true;

        // ─── State ────────────────────────────────────────────────────────────

        private bool    _visible;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _bgStyle;

        // Cached scene references — populated lazily
        private SurvivorController[] _survivors;
        private KillerController     _killer;
        private GeneratorInteraction[] _generators;
        private HookPoint[]           _hooks;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
#if !DEBUG && !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Disable entirely in release builds
            gameObject.SetActive(false);
            return;
#endif
            _visible = _showOnStart;

            EventBus.Subscribe<MatchStartedEvent>(_ => CacheReferences());
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MatchStartedEvent>(_ => CacheReferences());
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
        }

        private void CacheReferences()
        {
            _survivors  = FindObjectsByType<SurvivorController>(FindObjectsSortMode.None);
            _killer     = FindFirstObjectByType<KillerController>();
            _generators = FindObjectsByType<GeneratorInteraction>(FindObjectsSortMode.None);
            _hooks      = FindObjectsByType<HookPoint>(FindObjectsSortMode.None);
        }

        // ─── GUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            // Lazy init styles (must be done inside OnGUI)
            if (_labelStyle == null) InitStyles();

            float panelW = 320f;
            float panelH = 460f;
            float x      = 10f;
            float y      = 10f;

            // Background
            GUI.color = _bgColor;
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x + 8, y + 8, panelW - 16, panelH - 16));

            // ── Header ────────────────────────────────────────────────────────
            GUILayout.Label("PROJECT: CONDEMNED  DEV HUD", _headerStyle);
            GUILayout.Label($"F1 to hide", _labelStyle);
            GUILayout.Space(6);

            // ── Match State ───────────────────────────────────────────────────
            var gsm = GameStateManager.Instance;
            if (gsm != null)
            {
                GUILayout.Label($"── MATCH ──────────────────", _headerStyle);
                GUILayout.Label($"State:     {gsm.CurrentState}", _labelStyle);
                GUILayout.Label($"Duration:  {gsm.MatchDuration:F1}s", _labelStyle);
                GUILayout.Label($"Survivors: {gsm.SurvivorsAlive} alive  " +
                                $"{gsm.SurvivorsEscaped} escaped  " +
                                $"{gsm.SurvivorsSacrificed} sacrificed", _labelStyle);
                GUILayout.Space(4);
            }

            // ── Generators ────────────────────────────────────────────────────
            GUILayout.Label($"── GENERATORS ─────────────", _headerStyle);

            if (_generators == null || _generators.Length == 0)
            {
                GUILayout.Label("  (none found in scene)", _labelStyle);
            }
            else
            {
                int totalGens = GameStateManager.Instance?.TotalGenerators ?? 5;
                int done      = GameStateManager.Instance?.GensCompleted ?? 0;
                GUILayout.Label($"  {done}/{totalGens} repaired", _labelStyle);

                foreach (var gen in _generators)
                {
                    if (gen == null) continue;
                    string bar    = ProgressBar(gen.Progress / 100f, 12);
                    string status = gen.IsRepaired ? "DONE" : $"{gen.RepairerCount} repairing";
                    GUILayout.Label($"  Gen {gen.name.PadRight(8)} [{bar}] {gen.Progress:F0}%  {status}",
                        _labelStyle);
                }
            }

            GUILayout.Space(4);

            // ── Survivors ─────────────────────────────────────────────────────
            GUILayout.Label($"── SURVIVORS ──────────────", _headerStyle);

            var hs = SurvivorHealthSystem.Instance;
            if (_survivors == null || _survivors.Length == 0)
            {
                GUILayout.Label("  (none found in scene)", _labelStyle);
            }
            else
            {
                foreach (var survivor in _survivors)
                {
                    if (survivor == null) continue;
                    int id = survivor.gameObject.GetInstanceID();
                    var health  = hs?.GetHealthState(id) ?? HitState.Healthy;
                    var hook    = hs?.GetHookStage(id)   ?? HookStage.None;
                    bool hidden = Gameplay.LockerInteraction.IsSurvivorHidden(id);
                    bool los    = LineOfSightDetector.Instance?.IsVisible(survivor) ?? false;

                    string state = health == HitState.Dying ? "DOWN" :
                                   health == HitState.Injured ? "INJ" : "OK";
                    string hookStr = hook != HookStage.None ? $" HOOK:{hook}" : "";
                    string flags  = (hidden ? " [LOCKER]" : "") + (los ? " [VISIBLE]" : "");

                    GUILayout.Label($"  {survivor.gameObject.name,-12} {state}{hookStr}{flags}",
                        _labelStyle);
                }
            }

            GUILayout.Space(4);

            // ── Killer ────────────────────────────────────────────────────────
            GUILayout.Label($"── KILLER ─────────────────", _headerStyle);

            if (_killer == null)
            {
                GUILayout.Label("  (not found in scene)", _labelStyle);
            }
            else
            {
                var pos       = _killer.transform.position;
                bool carrying = HookSystem.Instance?.IsCarrying(_killer) ?? false;
                GUILayout.Label($"  Pos: ({pos.x:F1}, {pos.z:F1})", _labelStyle);
                GUILayout.Label($"  Stunned: {_killer.IsStunned}  " +
                                $"Lunging: {_killer.IsLunging}  " +
                                $"Carrying: {carrying}", _labelStyle);
            }

            GUILayout.Space(4);

            // ── Hooks ─────────────────────────────────────────────────────────
            GUILayout.Label($"── HOOKS ──────────────────", _headerStyle);
            if (_hooks == null || _hooks.Length == 0)
            {
                GUILayout.Label("  (none found in scene)", _labelStyle);
            }
            else
            {
                foreach (var hook in _hooks)
                {
                    if (hook == null) continue;
                    GUILayout.Label($"  Hook {hook.HookIndex}  {hook.OccupancyState}", _labelStyle);
                }
            }

            GUILayout.Space(4);
            GUILayout.Label($"── CONTROLS ───────────────", _headerStyle);
            GUILayout.Label("WASD=Move  LShift=Sprint  LCtrl=Crouch", _labelStyle);
            GUILayout.Label("Space=Interact/SkillCheck  F1=Hide HUD", _labelStyle);
            GUILayout.Label("Killer: LMB=Attack  RMB=Lunge  Space=Interact", _labelStyle);

            GUILayout.EndArea();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static string ProgressBar(float t, int width)
        {
            int filled = Mathf.RoundToInt(t * width);
            return new string('█', filled) + new string('░', width - filled);
        }

        private void InitStyles()
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = _fontSize,
                fontStyle = FontStyle.Normal,
            };
            _labelStyle.normal.textColor = Color.white;

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
            };
            _headerStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
        }
    }
}
