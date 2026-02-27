# Unity Package Dependencies

Packages required for Project: Condemned.
Install all via **Window → Package Manager** in Unity.

## Core (install first)

| Package | Version | Source | Notes |
|---|---|---|---|
| Input System | 1.8.x | Unity Registry | New input system. Disable old input in Project Settings. |
| Cinemachine | 3.x | Unity Registry | Camera controller. Use CM3 (not legacy CM2). |
| ProBuilder | 6.x | Unity Registry | Greybox environment prototyping. Remove when art kit is complete. |
| TextMeshPro | (built-in) | Unity Registry | All in-game text. Import TMP Essentials when prompted. |

## Networking

| Package | Version | Source | Notes |
|---|---|---|---|
| Netcode for GameObjects | 2.x | Unity Registry | Core P2P networking. |
| Unity Transport | 2.x | Unity Registry | Required by NGO. |
| Multiplayer Tools | 2.x | Unity Registry | Network profiler, simulator. Dev builds only. |

## Unity Gaming Services (UGS)

| Package | Version | Source | Notes |
|---|---|---|---|
| Authentication | 3.x | Unity Registry | Required by multiplayer services. Currently installed. |
| **Multiplayer Services** | TBD at M4 | Unity Registry | `com.unity.services.multiplayer` — unified replacement for deprecated Lobby (1.x) and Relay (1.x) packages. Add when starting M4. |

> ⚠️ `com.unity.services.lobby` and `com.unity.services.relay` are **deprecated** in Unity 6. Do not add them. Use `com.unity.services.multiplayer` at M4 instead.

## Navigation

| Package | Version | Source | Notes |
|---|---|---|---|
| AI Navigation | 2.x | Unity Registry | NavMesh for bot pathfinding. Replaces legacy NavMesh. |

## Setup Notes

1. After installing **Input System**, Unity will prompt to restart and disable the old system. Accept both.
2. After installing **TextMeshPro**, go to **Window → TextMeshPro → Import TMP Essential Resources**.
3. **UGS packages** require a linked Unity project ID: **Edit → Project Settings → Services**.
4. The **Multiplayer Tools** package includes the **Network Simulator** — essential for M4 latency testing.

## Version Pinning

Once all packages are installed, copy the exact versions from **Packages/manifest.json** and **Packages/packages-lock.json** into this file. Never upgrade packages mid-milestone without testing.
