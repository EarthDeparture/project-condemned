// ============================================================================
// GameSceneBootstrapper.cs
// Namespace: Condemned.Runtime
// Description: Scene-level initializer for the Game scene. Runs on Awake,
//              finds every system and character in the scene, wires up all
//              cross-system references, and starts the match.
//
//              This eliminates the need for manual Inspector wiring across
//              multiple components. The GameSceneSetup Editor tool populates
//              this component's serialized fields once; at runtime it handles
//              the rest automatically.
//
// Usage:
//   1. Run Condemned > Setup Game Scene from the Unity menu (one time).
//   2. Hit Play. The bootstrapper does everything else.
// ============================================================================

using System.Collections;
using System.Linq;
using UnityEngine;
using Condemned.Core;
using Condemned.Gameplay;
using Condemned.Systems;

namespace Condemned.Runtime
{
    [AddComponentMenu("Condemned/Runtime/Game Scene Bootstrapper")]
    public class GameSceneBootstrapper : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Match Config")]
        [Tooltip("Seconds between scene load and match start (gives physics time to settle).")]
        [SerializeField] private float _startDelay = 1.5f;

        [Tooltip("Number of survivors in this match. Must match actual survivors in scene.")]
        [SerializeField] private int _survivorCount = 1; // default 1 for solo dev testing

        [Header("Scene References (auto-found if left empty)")]
        [SerializeField] private Camera                  _mainCamera;
        [SerializeField] private SurvivorController[]    _survivors;
        [SerializeField] private KillerController        _killer;
        [SerializeField] private HookPoint[]             _hookPoints;
        [SerializeField] private GeneratorInteraction[]  _generators;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            AutoDiscover();
            WireReferences();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(_startDelay);
            BeginMatch();
        }

        // ─── Auto-Discovery ───────────────────────────────────────────────────

        /// <summary>
        /// For any serialized field left empty, find objects of that type in
        /// the scene automatically. This allows Play without any manual wiring.
        /// </summary>
        private void AutoDiscover()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_survivors == null || _survivors.Length == 0)
                _survivors = FindObjectsByType<SurvivorController>(FindObjectsSortMode.None);

            if (_killer == null)
                _killer = FindFirstObjectByType<KillerController>();

            if (_hookPoints == null || _hookPoints.Length == 0)
                _hookPoints = FindObjectsByType<HookPoint>(FindObjectsSortMode.None);

            if (_generators == null || _generators.Length == 0)
                _generators = FindObjectsByType<GeneratorInteraction>(FindObjectsSortMode.None);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Bootstrapper] Discovered: " +
                      $"{_survivors?.Length ?? 0} survivors, " +
                      $"killer={((_killer != null) ? "YES" : "NO")}, " +
                      $"{_hookPoints?.Length ?? 0} hooks, " +
                      $"{_generators?.Length ?? 0} generators, " +
                      $"camera={((_mainCamera != null) ? "YES" : "NO")}");
#endif
        }

        // ─── Wiring ───────────────────────────────────────────────────────────

        private void WireReferences()
        {
            WireCamera();
            WireSurvivors();
            WireKiller();
            WireHooks();
        }

        private void WireCamera()
        {
            if (_mainCamera == null) return;

            var cam = _mainCamera.GetComponent<IsometricCameraController>();
            if (cam == null)
                cam = _mainCamera.gameObject.AddComponent<IsometricCameraController>();

            // Set target to first survivor found
            if (_survivors != null && _survivors.Length > 0)
                cam.SetTarget(_survivors[0].transform);

            // Give controllers a camera reference
            if (_survivors != null)
                foreach (var s in _survivors)
                    s.SetCamera(_mainCamera.transform);

            if (_killer != null)
                _killer.SetCamera(_mainCamera.transform);
        }

        private void WireSurvivors()
        {
            if (_survivors == null) return;

            int count = 0;
            foreach (var survivor in _survivors)
            {
                if (survivor == null) continue;

                SurvivorHealthSystem.Instance?.RegisterSurvivor(survivor);
                LineOfSightDetector.Instance?.RegisterSurvivor(survivor);
                count++;
            }

            _survivorCount = Mathf.Max(1, count);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Bootstrapper] Registered {count} survivors with health + LOS systems.");
#endif
        }

        private void WireKiller()
        {
            if (_killer == null)
            {
#if DEBUG || DEVELOPMENT_BUILD
                Debug.LogWarning("[Bootstrapper] No KillerController found in scene.");
#endif
                return;
            }

            LineOfSightDetector.Instance?.SetKiller(_killer);
        }

        private void WireHooks()
        {
            if (_hookPoints == null) return;

            foreach (var hook in _hookPoints)
                HookSystem.Instance?.RegisterHook(hook);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Bootstrapper] Registered {_hookPoints.Length} hooks.");
#endif
        }

        // ─── Match Start ──────────────────────────────────────────────────────

        private void BeginMatch()
        {
            int actualSurvivors = _survivors?.Length ?? _survivorCount;
            GameStateManager.Instance?.StartMatch(actualSurvivors);

#if DEBUG || DEVELOPMENT_BUILD
            Debug.Log($"[Bootstrapper] Match started with {actualSurvivors} survivor(s).");
#endif
        }

        // ─── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("DEBUG: Re-discover scene objects")]
        private void Debug_Rediscover() => AutoDiscover();

        [ContextMenu("DEBUG: Force begin match now")]
        private void Debug_ForceBegin() => BeginMatch();
#endif
    }
}
