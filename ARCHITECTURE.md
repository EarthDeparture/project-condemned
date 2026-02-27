# ARCHITECTURE.md — Project: Condemned

## Project Overview

Isometric 1v4 asymmetric horror game built in Unity 6.
One killer hunts four survivors. Survivors repair generators and escape. The killer sacrifices them.

**Engine:** Unity 6 (URP)  
**Networking:** Unity Netcode for GameObjects + Unity Relay (P2P)  
**Scripting Backend:** IL2CPP  
**Target Platform:** Windows 64-bit (initial), expandable  

---

## Scene Flow

```
Bootstrap (persistent)
    │
    ├── MainMenu
    │       └── Lobby
    │               └── Game ──► EndScreen
    │                               └── Lobby (return)
    └── (managers persist via DontDestroyOnLoad)
```

- **Bootstrap** — initializes singletons (GameManager, AudioManager, NetworkManager). Never unloads.
- **MainMenu** — title screen, settings, play button.
- **Lobby** — role selection, perk equip, ready state, host/join.
- **Game** — the match. Procedurally generated map. Host-authoritative.
- **EndScreen** — results, scores, grades. Returns to Lobby.

---

## Folder Structure

```
Assets/
├── Scripts/
│   ├── Core/           # Singletons, base classes, event bus, utilities
│   ├── Gameplay/       # Player controllers, interactions, mechanics
│   ├── Systems/        # GameStateManager, PerkSystem, SkillCheck, etc.
│   ├── UI/             # All UI controllers and HUD logic
│   ├── Data/           # ScriptableObject definitions (perks, sounds, bots, etc.)
│   ├── Networking/     # NGO wrappers, RPC handlers, lobby management
│   └── Editor/         # Editor-only tools, Gizmos, custom inspectors
├── Art/
│   ├── Characters/     # Character models, rigs, materials
│   ├── Environment/    # Tile modules, props, terrain
│   ├── VFX/            # Particle systems, shaders
│   └── UI/             # Sprites, atlases, fonts
├── Audio/
│   ├── Music/          # Layered chase music, stingers
│   ├── SFX/            # All sound effects
│   └── Ambient/        # Environment loops, random stingers
├── Scenes/             # All Unity scenes
├── Prefabs/
│   ├── Characters/     # Killer and survivor prefabs (incl. NetworkObject)
│   ├── Environment/    # Tile modules, props, hooks, generators
│   ├── UI/             # UI panel prefabs
│   └── Systems/        # Manager prefabs, spawner prefabs
├── Resources/          # Runtime-loaded assets (use sparingly)
├── Settings/           # URP pipeline assets, Input System asset, quality settings
└── StreamingAssets/    # External data files (if needed)
```

---

## Key Systems & Ownership

| System | Location | Description |
|---|---|---|
| `GameStateManager` | `Scripts/Systems/` | Host-authoritative state machine. Single source of truth for match state. |
| `EventBus` | `Scripts/Core/` | Static typed event bus. All cross-system communication goes through here. |
| `AudioManager` | `Scripts/Systems/` | Singleton. All audio played via `AudioManager.Play(SoundEvent, position)`. |
| `NetworkManager` | `Scripts/Networking/` | NGO NetworkManager wrapper. Handles host/client lifecycle. |
| `MapGenerator` | `Scripts/Systems/` | Procedural tile placement. Runs on host only. Seeds stored in match data. |
| `PerkManager` | `Scripts/Systems/` | Loads equipped perks at match start. Subscribes to event bus. |
| `BotController` | `Scripts/Gameplay/` | Behaviour tree runner. Host-only. |
| `SkillCheckManager` | `Scripts/Systems/` | Decoupled mini-game. Accepts difficulty params, fires result events. |

---

## Networking Model

- **Transport:** Unity Relay (P2P, no dedicated server)
- **Authority:** Host-authoritative for all game state, gen progress, hook states
- **Movement:** Client-side prediction + host reconciliation
- **Bots:** Run on host only — never networked as player objects
- **Disconnect:** Client drops → bot backfill. Host drops → match ends gracefully.

---

## Data Architecture

All game configuration lives in **ScriptableObjects** — never magic numbers in code.

Key SO types:

| Type | Location | Purpose |
|---|---|---|
| `PerkDefinition` | `Data/Perks/` | Name, description, type, trigger event, effect |
| `SoundEvent` | `Data/Audio/` | Clip, volume range, pitch range, spatial blend |
| `BotDifficultyConfig` | `Data/Bots/` | Reaction time, accuracy, perception range per tier |
| `KillerDefinition` | `Data/Characters/` | Name, base speed, terror radius, power reference |
| `SurvivorDefinition` | `Data/Characters/` | Name, model reference, colour variant |
| `MapConfig` | `Data/Maps/` | Tile weights, ambient profile, theme |
| `TileModule` | `Data/Maps/` | Module type, prop manifest, navmesh connection points |

---

## Editor Conventions

- All editor-only code lives in `Scripts/Editor/` or wrapped in `#if UNITY_EDITOR`
- Use `Gizmos` for spatial debugging (hitboxes, nav radii, spawn zones)
- Custom inspectors for complex ScriptableObjects (PerkDefinition, TileModule)
- Debug HUD overlay: `NetworkDebugHUD` (editor builds only, stripped in release)

---

## Branch Strategy

| Branch | Purpose |
|---|---|
| `main` | Stable, tested. CI must pass. |
| `develop` | Integration branch. All features merge here first. |
| `feature/issue-XX-short-name` | One branch per GitHub issue. |
| `hotfix/description` | Urgent fixes directly to main. |

PRs from `feature/*` → `develop`. Periodic `develop` → `main` after milestone sign-off.
