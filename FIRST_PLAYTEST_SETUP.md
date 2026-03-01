# First Playtest Setup Guide

This guide will walk you through setting up the first playable prototype of Project: Condemned.

---

## Prerequisites

- Unity 6 (6000.3.10f1) installed and project opened
- Git latest changes pulled (`git pull origin main`)
- ~30 minutes of your time

---

## Step 1: Create All Prefabs (1 minute)

Open Unity and navigate to the top menu:

```
Condemned > Create All Playtest Prefabs
```

This will automatically create all necessary prefabs in `Assets/Prefabs/`:
- `Characters/Survivor.prefab`
- `Characters/Killer.prefab`
- `Environment/HookPoint.prefab`
- `Environment/Generator.prefab`
- `Environment/Pallet.prefab`
- `Environment/Vault.prefab`
- `Environment/Locker.prefab`
- `Environment/ExitGate.prefab`
- `Environment/Hatch.prefab`

**Verify:** Check `Assets/Prefabs/Characters/` and `Assets/Prefabs/Environment/` to confirm all prefabs exist.

---

## Step 2: Configure Input Actions (5 minutes)

### 2.1 Open the Input Actions Asset

Navigate to: `Assets/Settings/CondemendInputActions.inputactions`

Double-click to open the Input Actions editor.

### 2.2 Create Action Maps

You should see two action maps. If they don't exist, create them:

1. Click the `+` next to "Action Maps"
2. Create:
   - **Survivor**
   - **Killer**

### 2.3 Configure Survivor Actions

Expand the **Survivor** action map and add these actions:

| Action Name | Type | Binding | Description |
|---|---|---|---|
| Move | Value 2D Vector | WASD | Movement |
| Sprint | Button | Left Shift | Hold to sprint |
| Crouch | Button | Left Control | Toggle crouch |
| Interact | Button | Space | Interact / Submit skill check |

**For each action:**
1. Click the `+` next to the action
2. Add binding (Path)
3. Set the correct keyboard key

**Detailed bindings:**

**Move:**
- Right-click > Add Binding > 2D Vector Composite
- Set: Up = W, Down = S, Left = A, Right = D

**Sprint:**
- Right-click > Add Binding > Key > Left Shift

**Crouch:**
- Right-click > Add Binding > Key > Left Ctrl

**Interact:**
- Right-click > Add Binding > Key > Space

### 2.4 Configure Killer Actions

Expand the **Killer** action map and add these actions:

| Action Name | Type | Binding | Description |
|---|---|---|---|
| Move | Value 2D Vector | WASD | Movement |
| Attack | Button | Left Mouse Button | Basic attack |
| Lunge | Button | Right Mouse Button | Lunge attack |
| Interact | Button | Space | Interact / Hook survivor |

**Detailed bindings:**

**Move:**
- Same as Survivor (WASD)

**Attack:**
- Right-click > Add Binding > Key > Left Mouse (or `Mouse Left`)

**Lunge:**
- Right-click > Add Binding > Key > Right Mouse (or `Mouse Right`)

**Interact:**
- Right-click > Add Binding > Key > Space

### 2.5 Save and Generate Class

1. Click **Save Asset** (Ctrl+S)
2. Click **Generate C# Class** (top right button)
3. Confirm generation (should create `CondemendInputActions.cs`)

---

## Step 3: Open and Setup Game Scene (5 minutes)

### 3.1 Open Game Scene

Navigate to: `Assets/Scenes/Game.unity`
Double-click to open.

### 3.2 Create Ground Plane

1. Right-click in Hierarchy > 3D Object > Plane
2. Name it: `Ground`
3. Scale it: `(50, 1, 50)`

### 3.3 Create Main Camera

If there's no Main Camera:
1. Right-click in Hierarchy > Camera
2. Name it: `Main Camera`
3. Set Position: `(0, 15, -10)`
4. Set Rotation: `(60, 0, 0)`

### 3.4 Add IsometricCameraController

1. Select `Main Camera`
2. In Inspector, click `Add Component`
3. Search and add: `Isometric Camera Controller`
4. Set:
   - `_target`: Drag the Survivor here (we'll create it next)
   - `_distance`: `20`
   - `_height`: `15`

### 3.5 Place Survivors

1. From `Assets/Prefabs/Characters/`, drag `Survivor` prefab into the scene 4 times
2. Name them: `Survivor_1`, `Survivor_2`, `Survivor_3`, `Survivor_4`
3. Position them in a square around `(0, 0, 0)`:
   - Survivor_1: `(-5, 0, -5)`
   - Survivor_2: `(5, 0, -5)`
   - Survivor_3: `(-5, 0, 5)`
   - Survivor_4: `(5, 0, 5)`

### 3.6 Place Killer

1. From `Assets/Prefabs/Characters/`, drag `Killer` prefab into the scene
2. Name it: `Killer`
3. Position: `(0, 0, 0)`

### 3.7 Place Generators

1. From `Assets/Prefabs/Environment/`, drag `Generator` prefab into the scene 5 times
2. Name them: `Generator_1` through `Generator_5`
3. Position them spread out:
   - Generator_1: `(-15, 0, 0)`
   - Generator_2: `(15, 0, 0)`
   - Generator_3: `(0, 0, -15)`
   - Generator_4: `(0, 0, 15)`
   - Generator_5: `(20, 0, 20)`

### 3.8 Place Hooks

1. From `Assets/Prefabs/Environment/`, drag `HookPoint` prefab into the scene 7 times
2. Position them around the map:
   - Hook_1: `(-10, 0, -10)`
   - Hook_2: `(10, 0, -10)`
   - Hook_3: `(-10, 0, 10)`
   - Hook_4: `(10, 0, 10)`
   - Hook_5: `(-20, 0, 0)`
   - Hook_6: `(20, 0, 0)`
   - Hook_7: `(0, 0, -20)`

### 3.9 Place Pallets

1. From `Assets/Prefabs/Environment/`, drag `Pallet` prefab into the scene 4 times
2. Position them between generator clusters:
   - Pallet_1: `(-5, 0, -10)`
   - Pallet_2: `(5, 0, -10)`
   - Pallet_3: `(-5, 0, 10)`
   - Pallet_4: `(5, 0, 10)`

### 3.10 Place Vaults

1. From `Assets/Prefabs/Environment/`, drag `Vault` prefab into the scene 3 times
2. Position them near hooks:
   - Vault_1: `(-12, 0, -5)`
   - Vault_2: `(12, 0, 5)`
   - Vault_3: `(0, 0, -12)`

### 3.11 Place Lockers

1. From `Assets/Prefabs/Environment/`, drag `Locker` prefab into the scene 3 times
2. Position them:
   - Locker_1: `(-15, 0, -15)`
   - Locker_2: `(15, 0, -15)`
   - Locker_3: `(0, 0, -18)`

### 3.12 Place Exit Gates

1. From `Assets/Prefabs/Environment/`, drag `ExitGate` prefab into the scene 2 times
2. Name them: `ExitGate_A`, `ExitGate_B`
3. Position them at opposite corners:
   - ExitGate_A: `(-25, 0, 0)`, Rotation: `(0, 90, 0)`
   - ExitGate_B: `(25, 0, 0)`, Rotation: `(0, -90, 0)`

### 3.13 Place Hatch

1. From `Assets/Prefabs/Environment/`, drag `Hatch` prefab into the scene
2. Position: `(0, 0, 22)`

---

## Step 4: Add System Singletons (2 minutes)

Create empty GameObjects for each singleton system:

1. Right-click in Hierarchy > Create Empty
2. Name it: `GameStateManager`
3. Add component: `Game State Manager`
4. Configure:
   - `_totalGenerators`: `5`

5. Create Empty > Name: `SurvivorHealthSystem` > Add `Survivor Health System`
6. Create Empty > Name: `HookSystem` > Add `Hook System`
7. Create Empty > Name: `CombatSystem` > Add `Combat System`
8. Create Empty > Name: `SkillCheckManager` > Add `Skill Check Manager`
9. Create Empty > Name: `LineOfSightDetector` > Add `Line Of Sight Detector`
10. Create Empty > Name: `GameSceneBootstrapper` > Add `Game Scene Bootstrapper`
11. Create Empty > Name: `DebugHUD` > Add `Debug HUD`

---

## Step 5: Assign Input Actions to Controllers (2 minutes)

### 5.1 Survivor Controllers

For each Survivor (Survivor_1 through Survivor_4):
1. Select the Survivor
2. In Inspector, find `Survivor Controller` component
3. Assign `_inputActions`:
   - Click the circle icon
   - Select: `CondemendInputActions` (from Assets/Settings/)

### 5.2 Killer Controller

1. Select the Killer
2. In Inspector, find `Killer Controller` component
3. Assign `_inputActions`: `CondemendInputActions`

### 5.3 Assign Camera References

For each Survivor and Killer:
1. Select the character
2. Find the Controller component
3. Assign `_cameraTransform`: `Main Camera`

---

## Step 6: Configure Combat System (1 minute)

1. Select `CombatSystem` GameObject
2. In Inspector, find `Combat System` component
3. Set `_survivorLayer`:
   - Click the layer dropdown
   - Create a new layer named `Survivor` (Layer 6)
4. Assign this layer to all Survivor prefabs:
   - Select each Survivor
   - In Inspector (top right), change Layer to `Survivor`

---

## Step 7: Add Lighting (2 minutes)

1. Right-click in Hierarchy > Light > Directional Light
2. Name it: `Sun`
3. Set Position: `(0, 50, 0)`
4. Set Rotation: `(50, -30, 0)`
5. Set:
   - `Intensity`: `1`
   - `Shadow Type`: `Soft Shadows`
   - `Shadow Resolution`: `High`

---

## Step 8: Test (Go!)

1. Save the scene (`Ctrl+S`)
2. Click the **Play** button at the top center

### What to Test:

**As Survivor:**
- WASD to move around
- Hold Left Shift to sprint
- Hold Left Ctrl to crouch
- Walk up to a Generator → hold Space to repair
- When skill check appears, press Space in the green zone
- Walk up to a Pallet → press Space to drop
- Run toward a Vault → automatically vault
- Walk into a Locker → press Space to enter

**As Killer (for now, move the Killer manually in Scene view):**
- The systems are working, but you're controlling Survivors with keyboard
- Killer will have full controls in multiplayer (M4)

**To test combat:**
1. In Scene view (not Play mode), manually move Killer near a Survivor
2. In Play mode, Killer doesn't have full controls yet (that's M4/M5)
3. But the health/hook systems are coded and ready

---

## Expected Behavior

✅ Survivors can move (WASD), sprint (Shift), crouch (Ctrl)
✅ Survivors can repair generators (hold Space)
✅ Skill checks appear and can be submitted (press Space)
✅ Generators track progress (5 total to activate exit gates)
✅ After 5 generators, exit gates should activate
✅ Survivors can drop pallets
✅ Survivors can vault through windows
✅ Survivors can hide in lockers
✅ Press F1 to see DebugHUD (gen progress, survivor health)

---

## Troubleshooting

**Problem:** Input Actions not working
- **Solution:** Make sure `CondemendInputActions.inputactions` is saved and class is generated. Check console for errors.

**Problem:** Survivors fall through ground
- **Solution:** Ensure Ground Plane is at Y=0 and has a collider.

**Problem:** Skill checks not appearing
- **Solution:** Check that Generator's `_interactionRange` is >= distance to Survivor (default 2.5 units).

**Problem:** DebugHUD not showing
- **Solution:** Press F1 in Play mode. Make sure DebugHUD component is in the scene.

---

## What's Next

This is a **single-player prototype**. You're testing that the core systems work:

- ✅ Movement
- ✅ Generator interaction
- ✅ Skill checks
- ✅ Pallet drop
- ✅ Vault
- ✅ Locker hide
- ✅ Exit gates

**Multiplayer (M4)** and **Killer controls (M5)** come next. For now, focus on validating the Survivor-side interactions.

---

## Progress

Once you complete this guide and successfully test all interactions:

**You have achieved:**
- ✅ Core DBD gameplay loop coded
- ✅ All major systems functional
- ✅ Ready for M3 (AI Bots) to add computer-controlled opponents

**Next milestone:** M3 — AI Bot Infrastructure
- Bot survivors that can repair gens, vault, hide
- Bot killer that can hunt and hook survivors
- Full bot-vs-bot matches for balance testing
