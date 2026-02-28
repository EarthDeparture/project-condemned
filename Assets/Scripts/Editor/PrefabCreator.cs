// ============================================================================
// PrefabCreator.cs
// Namespace: Condemned.Editor
// Description: One-time editor script to create Survivor and Killer prefabs
//              with all necessary components assigned.
// ============================================================================

using UnityEngine;
using UnityEditor;

namespace Condemned.Editor
{
    public class PrefabCreator : EditorWindow
    {
        [MenuItem("Condemned/Create All Playtest Prefabs")]
        public static void CreateAllPrefabs()
        {
            CreateSurvivorPrefab();
            CreateKillerPrefab();
            CreateHookPointPrefab();
            CreateGeneratorPrefab();
            CreatePalletPrefab();
            CreateVaultPrefab();
            CreateLockerPrefab();
            CreateExitGatePrefab();
            CreateHatchPrefab();

            Debug.Log("[PrefabCreator] All playtest prefabs created successfully!");
        }

        private static void CreateSurvivorPrefab()
        {
            // Create the Survivor GameObject
            GameObject survivorGO = new GameObject("Survivor");

            // Add Capsule for visualization
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Mesh";
            capsule.transform.SetParent(survivorGO.transform);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = Vector3.one;

            // Remove the existing CapsuleCollider (we'll add CharacterController)
            DestroyImmediate(capsule.GetComponent<CapsuleCollider>());

            // Add CharacterController
            CharacterController cc = survivorGO.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);

            // Add SurvivorController
            var survivorController = survivorGO.AddComponent<Condemned.Gameplay.SurvivorController>();

            // Create InteractionOrigin (eye level for raycasts)
            GameObject interactionOrigin = new GameObject("InteractionOrigin");
            interactionOrigin.transform.SetParent(survivorGO.transform);
            interactionOrigin.transform.localPosition = new Vector3(0, 1.6f, 0);

            // Use reflection to set the private _interactionOrigin field
            var field = typeof(Condemned.Gameplay.SurvivorController).GetField("_interactionOrigin",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(survivorController, interactionOrigin.transform);

            // Save as prefab
            string path = "Assets/Prefabs/Characters/Survivor.prefab";
            PrefabUtility.SaveAsPrefabAsset(survivorGO, path);
            DestroyImmediate(survivorGO);

            Debug.Log($"[PrefabCreator] Created Survivor prefab at {path}");
        }

        private static void CreateKillerPrefab()
        {
            GameObject killerGO = new GameObject("Killer");

            // Add Capsule for visualization
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Mesh";
            capsule.transform.SetParent(killerGO.transform);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(1, 1.1f, 1); // Slightly taller

            // Remove the existing CapsuleCollider
            DestroyImmediate(capsule.GetComponent<CapsuleCollider>());

            // Add CharacterController
            CharacterController cc = killerGO.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 1.0f, 0);

            // Add KillerController
            var killerController = killerGO.AddComponent<Condemned.Gameplay.KillerController>();

            // Create AttackOrigin
            GameObject attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(killerGO.transform);
            attackOrigin.transform.localPosition = new Vector3(0, 1.2f, 0.5f);

            // Use reflection to set the private _attackOrigin field
            var field = typeof(Condemned.Gameplay.KillerController).GetField("_attackOrigin",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(killerController, attackOrigin.transform);

            // Save as prefab
            string path = "Assets/Prefabs/Characters/Killer.prefab";
            PrefabUtility.SaveAsPrefabAsset(killerGO, path);
            DestroyImmediate(killerGO);

            Debug.Log($"[PrefabCreator] Created Killer prefab at {path}");
        }

        private static void CreateHookPointPrefab()
        {
            GameObject hookGO = new GameObject("HookPoint");

            // Add visual (cylinder for post)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(hookGO.transform);
            visual.transform.localPosition = new Vector3(0, 0.75f, 0);
            visual.transform.localScale = new Vector3(0.1f, 1.5f, 0.1f);
            DestroyImmediate(visual.GetComponent<CapsuleCollider>());

            // Add BoxCollider for interaction
            BoxCollider collider = hookGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 2f, 1f);
            collider.center = new Vector3(0, 1f, 0);

            // Add HookPoint component
            hookGO.AddComponent<Condemned.Gameplay.HookPoint>();

            string path = "Assets/Prefabs/Environment/HookPoint.prefab";
            PrefabUtility.SaveAsPrefabAsset(hookGO, path);
            DestroyImmediate(hookGO);

            Debug.Log($"[PrefabCreator] Created HookPoint prefab at {path}");
        }

        private static void CreateGeneratorPrefab()
        {
            GameObject genGO = new GameObject("Generator");

            // Add visual (cube)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(genGO.transform);
            visual.transform.localPosition = new Vector3(0, 0.5f, 0);
            visual.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

            // Add BoxCollider
            BoxCollider collider = genGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1.5f, 2f);
            collider.center = new Vector3(0, 0.75f, 0);

            // Add GeneratorInteraction
            genGO.AddComponent<Condemned.Gameplay.GeneratorInteraction>();

            string path = "Assets/Prefabs/Environment/Generator.prefab";
            PrefabUtility.SaveAsPrefabAsset(genGO, path);
            DestroyImmediate(genGO);

            Debug.Log($"[PrefabCreator] Created Generator prefab at {path}");
        }

        private static void CreatePalletPrefab()
        {
            GameObject palletGO = new GameObject("Pallet");

            // Add visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(palletGO.transform);
            visual.transform.localPosition = new Vector3(0, 0.1f, 0);
            visual.transform.localScale = new Vector3(2f, 0.2f, 0.5f);

            // Add BoxCollider
            BoxCollider collider = palletGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 0.5f, 0.5f);
            collider.center = new Vector3(0, 0.25f, 0);

            // Add PalletInteraction
            palletGO.AddComponent<Condemned.Gameplay.PalletInteraction>();

            string path = "Assets/Prefabs/Environment/Pallet.prefab";
            PrefabUtility.SaveAsPrefabAsset(palletGO, path);
            DestroyImmediate(palletGO);

            Debug.Log($"[PrefabCreator] Created Pallet prefab at {path}");
        }

        private static void CreateVaultPrefab()
        {
            GameObject vaultGO = new GameObject("Vault");

            // Add wall visual
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(vaultGO.transform);
            wall.transform.localPosition = new Vector3(0, 1.5f, 0);
            wall.transform.localScale = new Vector3(0.2f, 3f, 2f);

            // Add exit points for vault calculation
            GameObject exit1 = new GameObject("ExitPoint_A");
            exit1.transform.SetParent(vaultGO.transform);
            exit1.transform.localPosition = new Vector3(-1, 0, 0);

            GameObject exit2 = new GameObject("ExitPoint_B");
            exit2.transform.SetParent(vaultGO.transform);
            exit2.transform.localPosition = new Vector3(1, 0, 0);

            // Add BoxCollider for the wall
            BoxCollider collider = vaultGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.2f, 3f, 2f);
            collider.center = new Vector3(0, 1.5f, 0);

            // Add VaultInteraction
            var vault = vaultGO.AddComponent<Condemned.Gameplay.VaultInteraction>();

            // Use reflection to set the exit points
            var field1 = typeof(Condemned.Gameplay.VaultInteraction).GetField("_exitA",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var field2 = typeof(Condemned.Gameplay.VaultInteraction).GetField("_exitB",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field1?.SetValue(vault, exit1.transform);
            field2?.SetValue(vault, exit2.transform);

            string path = "Assets/Prefabs/Environment/Vault.prefab";
            PrefabUtility.SaveAsPrefabAsset(vaultGO, path);
            DestroyImmediate(vaultGO);

            Debug.Log($"[PrefabCreator] Created Vault prefab at {path}");
        }

        private static void CreateLockerPrefab()
        {
            GameObject lockerGO = new GameObject("Locker");

            // Add visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(lockerGO.transform);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(1f, 2f, 0.8f);

            // Add BoxCollider
            BoxCollider collider = lockerGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 2f, 0.8f);
            collider.center = new Vector3(0, 1f, 0);

            // Add LockerInteraction
            lockerGO.AddComponent<Condemned.Gameplay.LockerInteraction>();

            string path = "Assets/Prefabs/Environment/Locker.prefab";
            PrefabUtility.SaveAsPrefabAsset(lockerGO, path);
            DestroyImmediate(lockerGO);

            Debug.Log($"[PrefabCreator] Created Locker prefab at {path}");
        }

        private static void CreateExitGatePrefab()
        {
            GameObject gateGO = new GameObject("ExitGate");

            // Add visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(gateGO.transform);
            visual.transform.localPosition = new Vector3(0, 2f, 0);
            visual.transform.localScale = new Vector3(3f, 4f, 0.3f);

            // Add BoxCollider
            BoxCollider collider = gateGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 4f, 1f);
            collider.center = new Vector3(0, 2f, 0);

            // Add ExitGateController
            gateGO.AddComponent<Condemned.Gameplay.ExitGateController>();

            string path = "Assets/Prefabs/Environment/ExitGate.prefab";
            PrefabUtility.SaveAsPrefabAsset(gateGO, path);
            DestroyImmediate(gateGO);

            Debug.Log($"[PrefabCreator] Created ExitGate prefab at {path}");
        }

        private static void CreateHatchPrefab()
        {
            GameObject hatchGO = new GameObject("Hatch");

            // Add visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(hatchGO.transform);
            visual.transform.localPosition = new Vector3(0, 0.05f, 0);
            visual.transform.localScale = new Vector3(0.8f, 0.1f, 0.8f);

            // Add BoxCollider for interaction
            BoxCollider collider = hatchGO.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.5f, 1f);
            collider.center = new Vector3(0, 0.25f, 0);

            // Add HatchController
            hatchGO.AddComponent<Condemned.Gameplay.HatchController>();

            string path = "Assets/Prefabs/Environment/Hatch.prefab";
            PrefabUtility.SaveAsPrefabAsset(hatchGO, path);
            DestroyImmediate(hatchGO);

            Debug.Log($"[PrefabCreator] Created Hatch prefab at {path}");
        }
    }
}
