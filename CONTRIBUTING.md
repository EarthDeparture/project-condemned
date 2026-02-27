# CONTRIBUTING.md — Project: Condemned

## Coding Standards

### Namespaces

All scripts must declare a namespace:

```csharp
namespace Condemned.Core        // Base classes, event bus, utilities
namespace Condemned.Gameplay    // Player controllers, interactions
namespace Condemned.Systems     // Game state, perks, skill checks, audio
namespace Condemned.UI          // All UI controllers
namespace Condemned.Data        // ScriptableObject definitions
namespace Condemned.Networking  // NGO wrappers, RPC handlers
```

---

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `GameStateManager` |
| Methods | PascalCase | `StartMatch()` |
| Properties | PascalCase | `CurrentState` |
| Private fields | `_camelCase` | `_currentHookStage` |
| Public fields | PascalCase | `TerrorRadius` |
| Constants | `UPPER_SNAKE` | `MAX_SURVIVORS` |
| Interfaces | `I` prefix | `IInteractable` |
| Abstract base classes | `Base` suffix | `BaseCharacterController` |
| ScriptableObjects | `Definition` or `Config` suffix | `PerkDefinition`, `BotDifficultyConfig` |
| Events | `On` prefix | `OnSurvivorHooked`, `OnGenCompleted` |
| Coroutines | `C_` prefix | `C_BleedOutTimer()` |
| NetworkVariables | `Net_` prefix | `Net_HookStage` |

---

### Script Header Template

Every new script must include this header:

```csharp
// ============================================================================
// [ClassName].cs
// Namespace: Condemned.[Module]
// Description: [One sentence description of responsibility]
// ============================================================================

using System;
using UnityEngine;
// ... other usings

namespace Condemned.[Module]
{
    public class ClassName : MonoBehaviour
    {
        // ...
    }
}
```

---

### Core Rules

**No magic numbers.** All tunable values live in ScriptableObjects or clearly named constants.

```csharp
// ❌ Wrong
if (distance < 32f) TriggerHeartbeat();

// ✅ Correct
if (distance < _killerDefinition.TerrorRadius) TriggerHeartbeat();
```

**Event bus for cross-system communication.** Never call into another system's public methods directly.

```csharp
// ❌ Wrong
AudioManager.Instance.PlayChaseMusic();

// ✅ Correct
EventBus.Publish(new ChaseStartedEvent(survivorId));
// AudioManager subscribes to ChaseStartedEvent internally
```

**No FindObjectOfType in runtime code.** Use dependency injection, singleton accessors, or event subscriptions.

```csharp
// ❌ Wrong (in Update or any hot path)
var mgr = FindObjectOfType<GameStateManager>();

// ✅ Correct
GameStateManager.Instance.CurrentState
```

**Null checks on NetworkObject references.** Always validate before acting on networked objects — they may have despawned.

```csharp
if (networkObject != null && networkObject.IsSpawned)
{
    // safe to act
}
```

---

### Editor & Debug Tooling

Wrap all editor-only and debug code in conditional compilation:

```csharp
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _terrorRadius);
    }
#endif

#if DEBUG || DEVELOPMENT_BUILD
    Debug.Log($"[GameState] Transition: {previous} → {next}");
#endif
```

Never ship with `Debug.Log` calls outside of `#if` guards.

---

### ScriptableObject Configuration Pattern

Any value that might need tuning goes in a ScriptableObject:

```csharp
[CreateAssetMenu(fileName = "New PerkDefinition", menuName = "Condemned/Perks/Perk Definition")]
public class PerkDefinition : ScriptableObject
{
    [Header("Identity")]
    public string PerkName;
    public string Description;
    public Sprite Icon;

    [Header("Behaviour")]
    public PerkType Type;            // Passive, Triggered, Active
    public GameEventType TriggerOn;  // OnHook, OnGenComplete, etc.
}
```

---

### Performance Rules

- **No `GetComponent` in `Update`.** Cache references in `Awake` or `Start`.
- **No `new` allocations in hot paths** (Update, FixedUpdate). Pre-allocate or use pools.
- **Physics queries use layer masks.** Never query all layers.
- **NavMesh queries are async or rate-limited.** Not every frame.
- **Audio: use the pool.** Never `Instantiate` an AudioSource at runtime.

---

### Git Workflow

1. Create a branch: `git checkout -b feature/issue-XX-short-name`
2. Commit often with clear messages:
   ```
   feat(gameplay): implement pallet drop and stun system (#15)
   fix(networking): correct hook state desync on reconnect (#37)
   chore(tooling): add GameCI Unity build workflow (#5)
   ```
3. Keep commits focused — one logical change per commit
4. Open a PR to `develop` when complete
5. CI must pass before merge
6. Delete branch after merge

**Commit prefix guide:**
- `feat` — new feature
- `fix` — bug fix
- `chore` — tooling, CI, documentation
- `refactor` — code change with no behaviour change
- `perf` — performance improvement
- `art` — art assets, models, textures
- `audio` — audio assets or audio system changes
- `balance` — tuning values, no logic change

---

### .meta Files

**Never delete .meta files.** They contain Unity's GUID registry — losing them breaks all scene and prefab references.

If a .meta file appears in a PR diff and no corresponding asset was changed, investigate before merging.

#### Adding new scripts outside the Unity Editor

When creating `.cs` files via CLI or a code editor (not through the Unity Editor UI), Unity will **not** auto-generate the `.meta` file. You must generate it manually before committing, or the `Compile Check` CI step will fail.

Use this helper script to generate a `.meta` file for any new script:

```bash
python3 -c "
import uuid, sys

path = sys.argv[1]   # e.g. Assets/Scripts/Gameplay/MySystem.cs
guid = uuid.uuid4().hex

content = f'''fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'''

with open(path + '.meta', 'w') as f:
    f.write(content)

print(f'Generated {path}.meta  (guid: {guid})')
" Assets/Scripts/Gameplay/MyNewSystem.cs
```

Commit the `.meta` file in the **same commit** as the `.cs` file. The CI enforces this pairing.
