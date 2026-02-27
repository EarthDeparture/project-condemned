# Project: Condemned

> Working title. Asymmetric 1v4 horror game — one killer, four survivors.

**Engine:** Unity 6 (URP)  
**Networking:** P2P via Unity Netcode for GameObjects + Unity Relay  
**Art Style:** 3D low-poly (LOD framework in place for high-fidelity upgrade)  
**Platform:** Windows PC (initial target)  

---

## Development Status

| Milestone | Status |
|---|---|
| M0 — Foundation & Infrastructure | 🔵 In Progress |
| M1 — Core Mechanics Prototype | ⬜ Todo |
| M2 — Game Loop & Rules Engine | ⬜ Todo |
| M3 — AI Bot Infrastructure | ⬜ Todo |
| M4 — Multiplayer Foundation (P2P) | ⬜ Todo |
| M5 — Chase, Combat & Status Systems | ⬜ Todo |
| M6 — Procedural Map System | ⬜ Todo |
| M7 — Art & Visual Pipeline (Low-Poly) | ⬜ Todo |
| M8 — Audio Systems | ⬜ Todo |
| M9 — UI/UX Systems | ⬜ Todo |
| M10 — Alpha Stability & Playtesting | ⬜ Todo |
| M11 — Content Expansion & Upgrade Path | ⬜ Todo |

Full task breakdown: [GitHub Project Board](https://github.com/users/EarthDeparture/projects/3)

---

## Getting Started (Development)

### Prerequisites

- Unity 6 (6000.x LTS) — install via [Unity Hub](https://unity.com/download)
- Git with LFS enabled
- Rider or Visual Studio 2022

### Setup

```bash
git clone https://github.com/EarthDeparture/project-condemned.git
cd project-condemned
```

Open Unity Hub → **Add project from disk** → select the cloned folder.

### CI Secrets Required

The GitHub Actions build workflow requires three repository secrets:

| Secret | Where to get it |
|---|---|
| `UNITY_LICENSE` | Run `gh secret set UNITY_LICENSE` — see [GameCI docs](https://game.ci/docs/github/activation) |
| `UNITY_EMAIL` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |

---

## Documentation

| File | Purpose |
|---|---|
| `ARCHITECTURE.md` | System overview, folder structure, networking model |
| `CONTRIBUTING.md` | Coding standards, naming conventions, git workflow |
| `Docs/packages.md` | Required Unity packages and version pins |
| `Docs/BALANCE.md` | Tuned gameplay values (created at M10) |
| `Docs/TILE_SPEC.md` | Tile module authoring guide (created at M6) |
| `Docs/ART_GUIDE.md` | Art style guide and polygon budgets (created at M7) |

---

## License

Private repository — all rights reserved.
