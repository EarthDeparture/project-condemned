// ============================================================================
// GameSceneSetup.cs
// Namespace: Condemned.Editor
// Description: One-click Game scene setup tool.
//
//              Menu: Condemned > Setup Game Scene
//
//              What it does:
//                1. Opens the Game scene (if not already open)
//                2. Creates all required manager GameObjects
//                3. Creates a playable Survivor capsule (player-controlled)
//                4. Creates a Killer capsule (player-controlled for dev testing)
//                5. Places 5 generator cubes across the map
//                6. Places 4 hook points
//                7. Places 2 exit gate objects
//                8. Places 1 hatch with spawn points
//                9. Ensures Main Camera has IsometricCameraController
//               10. Wires the InputActions asset to all controllers
//               11. Adds all 5 scenes to EditorBuildSettings
//               12. Marks the scene dirty and saves
//
//              Run this ONCE before pressing Play. If objects already exist,
//              they are left untouched (idempotent for most steps).
// ============================================================================

#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Condemned.Gameplay;
using Condemned.Systems;
using Condemned.Runtime;
using Condemned.UI;

namespace Condemned.Editor
{
    public static class GameSceneSetup
    {
        private const string GameScenePath      = "Assets/Scenes/Game.unity";
        private const string InputActionsPath   = "Assets/Settings/CondemendInputActions.inputactions";
        private const string LogPrefix          = "[GameSceneSetup]";

        // ─── Menu Entry ───────────────────────────────────────────────────────

        [MenuItem("Condemned/Setup Game Scene", priority = 10)]
        public static void SetupGameScene()
        {
            // Load the Game scene if it's not already active
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != GameScenePath)
            {
                bool opened = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (!opened) return;
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            }

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
                Debug.LogWarning($"{LogPrefix} InputActions not found at {InputActionsPath}. " +
                                 "Controllers will need manual assignment.");

            EnsureManagers();
            EnsureSurvivor(inputActions);   // Survivor first — camera needs its transform
            EnsureKiller(inputActions);
            EnsureCamera(inputActions);     // Camera last — wires target to existing Survivor
            EnsureGenerators();
            EnsureHooks();
            EnsureExitGates();
            EnsureHatch();
            SetupBuildSettings();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log($"{LogPrefix} ✓ Game scene setup complete. Press Play to test.");
        }

        [MenuItem("Condemned/Clear Game Scene Objects", priority = 11)]
        public static void ClearSetupObjects()
        {
            if (!EditorUtility.DisplayDialog("Clear Setup Objects",
                    "Delete all GameSceneSetup-created objects from the active scene?",
                    "Delete", "Cancel"))
                return;

            string[] tags = { "Condemned_Manager", "Condemned_Survivor",
                               "Condemned_Killer",  "Condemned_Generator",
                               "Condemned_Hook",    "Condemned_ExitGate", "Condemned_Hatch" };

            int count = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (Array.Exists(tags, t => root.name.StartsWith(t) || root.CompareTag("EditorOnly")))
                {
                    Undo.DestroyObjectImmediate(root);
                    count++;
                }
            }

            Debug.Log($"{LogPrefix} Removed {count} setup objects.");
        }

        // ─── Manager Setup ────────────────────────────────────────────────────

        private static void EnsureManagers()
        {
            var managersGO = FindOrCreate("_Managers");

            EnsureComponent<GameStateManager>(managersGO);
            EnsureComponent<SkillCheckManager>(managersGO);
            EnsureComponent<LineOfSightDetector>(managersGO);
            EnsureComponent<HookSystem>(managersGO);
            EnsureComponent<SurvivorHealthSystem>(managersGO);
            EnsureComponent<CombatSystem>(managersGO);
            EnsureComponent<GameSceneBootstrapper>(managersGO);
            EnsureComponent<DebugHUD>(managersGO);

            Log("Managers OK");
        }

        // ─── Camera Setup ─────────────────────────────────────────────────────

        private static void EnsureCamera(InputActionAsset inputActions)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camGO, "Create Main Camera");
                Log("Created Main Camera");
            }

            var camCtrl = EnsureComponent<IsometricCameraController>(cam.gameObject);

            // Wire the camera target directly via SerializedObject so it persists
            // in the saved scene — no runtime bootstrapping required for camera to work.
            var survivorGO = GameObject.Find("Survivor_Player");
            if (survivorGO != null)
            {
                var so         = new SerializedObject(camCtrl);
                var targetProp = so.FindProperty("_target");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = survivorGO.transform;
                    so.ApplyModifiedProperties();
                    Log("Camera target → Survivor_Player");
                }
            }
            else
            {
                Log("WARNING: Survivor_Player not found. Camera target unset — Bootstrapper will wire it at runtime.");
            }

            Log("Camera OK");
        }

        // ─── Survivor Setup ───────────────────────────────────────────────────

        private static void EnsureCharacter<T>(string goName, Vector3 position,
                                                Color gizmoColor, InputActionAsset inputActions,
                                                Action<T, GameObject> configure) where T : Component
        {
            var existing = GameObject.Find(goName);
            if (existing != null)
            {
                Log($"{goName} already in scene — skipped.");
                return;
            }

            // Capsule gives a visible body and works with CharacterController
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = goName;
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");

            // Colour the capsule to distinguish survivor/killer
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = gizmoColor;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // CharacterController replaces the default capsule collider
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.AddComponent<CharacterController>();

            var component = go.AddComponent<T>();

            // Interaction origin child
            var origin = new GameObject("InteractionOrigin");
            origin.transform.SetParent(go.transform);
            origin.transform.localPosition = new Vector3(0f, 0.8f, 0.6f);

            configure(component, go);

            // Wire InputActions via SerializedObject
            if (inputActions != null)
            {
                var so = new SerializedObject(component);
                var prop = so.FindProperty("_inputActions");
                if (prop != null)
                {
                    prop.objectReferenceValue = inputActions;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static void EnsureSurvivor(InputActionAsset inputActions)
        {
            EnsureCharacter<SurvivorController>(
                "Survivor_Player",
                new Vector3(0f, 1f, 4f),
                new Color(0.3f, 0.7f, 1f),
                inputActions,
                (ctrl, go) =>
                {
                    // Wire interactionOrigin
                    var so   = new SerializedObject(ctrl);
                    var prop = so.FindProperty("_interactionOrigin");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = go.transform.Find("InteractionOrigin");
                        so.ApplyModifiedProperties();
                    }
                });

            Log("Survivor OK");
        }

        private static void EnsureKiller(InputActionAsset inputActions)
        {
            EnsureCharacter<KillerController>(
                "Killer_Player",
                new Vector3(0f, 1f, -6f),
                new Color(0.9f, 0.2f, 0.2f),
                inputActions,
                (ctrl, go) =>
                {
                    // Wire attackOrigin
                    var so   = new SerializedObject(ctrl);
                    var prop = so.FindProperty("_attackOrigin");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = go.transform.Find("InteractionOrigin");
                        so.ApplyModifiedProperties();
                    }
                });

            Log("Killer OK");
        }

        // ─── Generators ───────────────────────────────────────────────────────

        private static void EnsureGenerators()
        {
            var genParent = FindOrCreate("Generators");

            // Place 5 generators in a rough spread around the map
            Vector3[] positions =
            {
                new Vector3( 8f, 0.5f,  8f),
                new Vector3(-8f, 0.5f,  8f),
                new Vector3( 8f, 0.5f, -8f),
                new Vector3(-8f, 0.5f, -8f),
                new Vector3( 0f, 0.5f, 15f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                string name = $"Generator_{i}";
                if (genParent.transform.Find(name) != null) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(genParent.transform);
                go.transform.position = positions[i];
                go.transform.localScale = new Vector3(1.2f, 1.6f, 0.8f);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.8f, 0.6f, 0.1f);
                go.GetComponent<Renderer>().sharedMaterial = mat;

                var gen = go.AddComponent<GeneratorInteraction>();

                var so   = new SerializedObject(gen);
                var prop = so.FindProperty("_genIndex");
                if (prop != null) { prop.intValue = i; so.ApplyModifiedProperties(); }
            }

            Log("Generators OK");
        }

        // ─── Hook Points ──────────────────────────────────────────────────────

        private static void EnsureHooks()
        {
            var hookParent = FindOrCreate("Hooks");

            Vector3[] positions =
            {
                new Vector3( 5f, 2.5f,  0f),
                new Vector3(-5f, 2.5f,  0f),
                new Vector3( 0f, 2.5f, 10f),
                new Vector3( 0f, 2.5f,-10f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                string name = $"Hook_{i}";
                if (hookParent.transform.Find(name) != null) continue;

                var go = new GameObject(name);
                go.transform.SetParent(hookParent.transform);
                go.transform.position = positions[i];
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                // Visible marker (small sphere)
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.SetParent(go.transform);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale    = Vector3.one * 0.3f;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.6f, 0.1f, 0.6f);
                marker.GetComponent<Renderer>().sharedMaterial = mat;

                // Interaction collider (larger, easier to raycast)
                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.6f;

                go.AddComponent<HookPoint>();
            }

            Log("Hooks OK");
        }

        // ─── Exit Gates ───────────────────────────────────────────────────────

        private static void EnsureExitGates()
        {
            var gateParent = FindOrCreate("ExitGates");

            Vector3[] positions = { new Vector3(20f, 2f, 0f), new Vector3(-20f, 2f, 0f) };

            for (int i = 0; i < positions.Length; i++)
            {
                string name = $"ExitGate_{i}";
                if (gateParent.transform.Find(name) != null) continue;

                var go = new GameObject(name);
                go.transform.SetParent(gateParent.transform);
                go.transform.position = positions[i];
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                // Visual gate frame
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Frame";
                frame.transform.SetParent(go.transform);
                frame.transform.localPosition = Vector3.zero;
                frame.transform.localScale    = new Vector3(3f, 4f, 0.3f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.2f, 0.2f);
                frame.GetComponent<Renderer>().sharedMaterial = mat;

                // Interaction collider on the frame
                go.AddComponent<BoxCollider>();

                // Escape zone trigger (behind the gate)
                var escapeTriggerGO = new GameObject("EscapeTrigger");
                escapeTriggerGO.transform.SetParent(go.transform);
                escapeTriggerGO.transform.localPosition = new Vector3(0f, 0f, 2f);
                var escapeTrigger = escapeTriggerGO.AddComponent<BoxCollider>();
                escapeTrigger.isTrigger = true;
                escapeTrigger.size      = new Vector3(3f, 4f, 0.5f);

                var gate = go.AddComponent<ExitGateController>();
                var so   = new SerializedObject(gate);

                var indexProp   = so.FindProperty("_gateIndex");
                var triggerProp = so.FindProperty("_escapeTrigger");
                if (indexProp   != null) { indexProp.intValue = i;                        }
                if (triggerProp != null) { triggerProp.objectReferenceValue = escapeTrigger; }
                so.ApplyModifiedProperties();
            }

            Log("Exit Gates OK");
        }

        // ─── Hatch ────────────────────────────────────────────────────────────

        private static void EnsureHatch()
        {
            if (GameObject.Find("Hatch") != null)
            {
                Log("Hatch already in scene — skipped.");
                return;
            }

            var go = new GameObject("Hatch");
            Undo.RegisterCreatedObjectUndo(go, "Create Hatch");

            // Mesh root (visible marker)
            var meshRoot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            meshRoot.name = "HatchMesh";
            meshRoot.transform.SetParent(go.transform);
            meshRoot.transform.localPosition = Vector3.zero;
            meshRoot.transform.localScale    = new Vector3(1f, 0.1f, 1f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0f, 0.9f, 0.5f);
            meshRoot.GetComponent<Renderer>().sharedMaterial = mat;

            // Interaction collider
            var col      = go.AddComponent<CapsuleCollider>();
            col.height   = 0.3f;
            col.radius   = 0.6f;
            col.center   = new Vector3(0f, 0.15f, 0f);

            // Spawn points (8 points around the map)
            var spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(go.transform);

            Vector3[] spawnPositions =
            {
                new Vector3( 5f, 0f,  5f), new Vector3(-5f, 0f,  5f),
                new Vector3( 5f, 0f, -5f), new Vector3(-5f, 0f, -5f),
                new Vector3(10f, 0f,  0f), new Vector3(-10f,0f,  0f),
                new Vector3( 0f, 0f, 10f), new Vector3( 0f, 0f,-10f),
            };

            var spawnTransforms = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                var pt = new GameObject($"SpawnPoint_{i}");
                pt.transform.SetParent(spawnRoot.transform);
                pt.transform.localPosition = spawnPositions[i];
                spawnTransforms[i] = pt.transform;
            }

            var hatch    = go.AddComponent<HatchController>();
            var so       = new SerializedObject(hatch);
            var spawnProp = so.FindProperty("_spawnPoints");
            var meshProp  = so.FindProperty("_hatchMeshRoot");
            var colProp   = so.FindProperty("_interactCollider");

            if (spawnProp != null)
            {
                spawnProp.ClearArray();
                spawnProp.arraySize = spawnTransforms.Length;
                for (int i = 0; i < spawnTransforms.Length; i++)
                    spawnProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnTransforms[i];
            }
            if (meshProp != null) meshProp.objectReferenceValue = meshRoot;
            if (colProp  != null) colProp.objectReferenceValue  = col;
            so.ApplyModifiedProperties();

            // Start hidden
            meshRoot.SetActive(false);
            col.enabled = false;

            Log("Hatch OK");
        }

        // ─── Build Settings ───────────────────────────────────────────────────

        private static void SetupBuildSettings()
        {
            var scenes = new[]
            {
                "Assets/Scenes/Bootstrap.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Lobby.unity",
                "Assets/Scenes/Game.unity",
                "Assets/Scenes/EndScreen.unity",
            };

            var buildScenes = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                buildScenes[i] = new EditorBuildSettingsScene(scenes[i], true);

            EditorBuildSettings.scenes = buildScenes;
            Log($"Build Settings: {scenes.Length} scenes registered.");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static GameObject FindOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void Log(string msg) =>
            Debug.Log($"{LogPrefix} {msg}");
    }
}
#endif
