# Project: Condemned

> Working title. Asymmetric 1v4 horror game — one killer, four survivors.

**Engine:** Unity 6 (6000.3.10f1, URP)  
**Networking:** P2P via Unity Netcode for GameObjects + Unity Relay  
**Art Style:** 3D low-poly (LOD framework in place for high-fidelity upgrade path)  
**Platform:** Windows PC (initial target)  

---

## Development Status

| Milestone | Status | Scope |
|---|---|---|
| M0 — Foundation & Infrastructure | ✅ Complete | Repo, CI, folder structure, core systems, scene flow |
| M1 — Core Mechanics Prototype | ✅ Complete | Camera, characters, generator, vault, pallet, locker, LOS |
| M2 — Game Loop & Rules Engine | ⬜ Todo | Win/loss conditions, hook stages, exit gates, hatch |
| M3 — AI Bot Infrastructure | ⬜ Todo | NavMesh agents, survivor bot, killer bot, sound awareness |
| M4 — Multiplayer Foundation (P2P) | ⬜ Todo | NGO integration, Relay, lobby, network-sync all M1/M2 objects |
| M5 — Chase, Combat & Status Systems | ⬜ Todo | Bloodlust, health states, hook, carry, perks stub |
| M6 — Procedural Map System | ⬜ Todo | Tile modules, runtime layout gen, LOD framework |
| M7 — Art & Visual Pipeline (Low-Poly) | ⬜ Todo | Character mesh, environment tiles, VFX, post-processing |
| M8 — Audio Systems | ⬜ Todo | Spatial audio, terror radius, ambience, footstep mixer |
| M9 — UI/UX Systems | ⬜ Todo | HUD, skill check dial, menus, lobby screen |
| M10 — Alpha Stability & Playtesting | ⬜ Todo | Balance pass, bug triage, performance profiling |
| M11 — Content Expansion & Upgrade Path | ⬜ Todo | Additional killers/maps, high-fidelity art swap |

Full task breakdown: [GitHub Project Board](https://github.com/users/EarthDeparture/projects/3)

---

## M1 — What's Built

All eight M1 issues are closed and merged to `main`. The following systems exist as fully coded, event-driven MonoBehaviours ready to be wired to prefabs:

| Script | System | Notes |
|---|---|---|
| `IsometricCameraController` | Fixed-angle isometric camera | Zoom, follow target, coroutine shake |
| `SurvivorController` | Survivor movement | Walk/sprint/crouch, interaction raycast, footstep events |
| `KillerController` | Killer movement | Lunge mechanic, stun handling, bloodlust hook for M5 |
| `GeneratorInteraction` | Generator repair | Multi-survivor stacking, skill check integration, killer kick |
| `SkillCheckManager` | Skill check mini-game | Rotating dial, configurable hit/great zones, result callbacks |
| `VaultInteraction` | Window vault | Survivor + killer vault, post-vault block, exit direction inference |
| `PalletInteraction` | Pallet drop/break | Standing → Dropped → Broken state machine, stun on drop |
| `LockerInteraction` | Locker hide | Enter/exit toggle, static hidden registry for LOS, killer pull |
| `LineOfSightDetector` | Killer LOS | 10Hz raycasts, locker bypass, `IsVisible()` API |

> **Note:** M1 produces code and systems only. Prefabs and scene wiring are M2/M3 work.
> The game is not yet in a playable state — see [Playability Status](#playability-status) below.

---

## Playability Status

**Current state: Not yet playable.**

The following remain before a first playable build is possible:

1. **Input Actions asset** — `CondemendInputActions.inputactions` must be configured in the Unity Input System with the correct action maps (`Survivor`: Move/Sprint/Crouch/Interact; `Killer`: Move/Attack/Lunge/Interact)
2. **Character prefabs** — Survivor and Killer prefabs need to be created with `CharacterController`, the controller scripts, and the Input Actions asset assigned
3. **Scene wiring** — `Game.unity` needs the prefabs placed, `IsometricCameraController` given a target, and `LineOfSightDetector`/`SkillCheckManager` added as scene objects
4. **Build Settings** — All 5 scenes must be added to `EditorBuildSettings` (Bootstrap=0, MainMenu=1, Lobby=2, Game=3, EndScreen=4)
5. **Interaction colliders** — Generator, vault, pallet, and locker prefabs need correctly-sized colliders for the interaction raycasts to hit

These are M2 tasks. The first playable prototype is the M2 deliverable.

---

## Getting Started (Development)

### Prerequisites

- Unity 6 (6000.3.10f1) — install via [Unity Hub](https://unity.com/download)
- Git LFS enabled (`git lfs install`)
- Rider or Visual Studio 2022 (both supported)

### Setup

```bash
git clone https://github.com/EarthDeparture/project-condemned.git
cd project-condemned
```

Open **Unity Hub** → **Add project from disk** → select the cloned folder.

---

## CI

Two workflows run on every push:

| Workflow | Trigger | Requirement |
|---|---|---|
| **Compile Check** | Every push / PR | None — always runs |
| **Unity Build CI** | Every push / PR / tags / manual | Requires `UNITY_LICENSE` secret — **skips gracefully** if not set |

The build job will be **skipped** (not failed) when `UNITY_LICENSE` is not configured. This keeps the CI green during active development.

### Setting Up Build Secrets (one-time)

See [Docs/CI_SETUP.md](Docs/CI_SETUP.md) for the full walkthrough.

| Secret | Source |
|---|---|
| `UNITY_LICENSE` | Personal license `.ulf` file via `activation.yml` workflow |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

---

## Documentation

| File | Purpose |
|---|---|
| `ARCHITECTURE.md` | System overview, folder structure, networking model, event bus usage |
| `CONTRIBUTING.md` | Coding standards, naming conventions, git workflow, meta file rules |
| `Docs/packages.md` | Required Unity packages and version pins |
| `Docs/CI_SETUP.md` | Unity license activation and CI secret setup |
| `Docs/BALANCE.md` | Tuned gameplay values *(created at M10)* |
| `Docs/TILE_SPEC.md` | Tile module authoring guide *(created at M6)* |
| `Docs/ART_GUIDE.md` | Art style guide and polygon budgets *(created at M7)* |

---

## License

Private repository — all rights reserved.
