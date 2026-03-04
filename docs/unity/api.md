# Unity API Reference

## MorphynController

The main component that loads and runs `.morph` files.

### Settings

| Setting | Description |
|---------|-------------|
| **Morphyn Scripts** | Drag `.morph` files here |
| **Run On Start** | Auto-load on `Start()` |
| **Enable Tick** | Send `tick(dt)` every frame |
| **Enable Hot Reload** | Edit files during Play mode |
| **Auto Save** | Save on quit |
| **Save Folder** | Where to save state |

### API Methods

```cs
MorphynController morphyn = MorphynController.Instance;

// Get field — typed helpers
float hp        = morphyn.GetFloat("Player", "hp");
bool alive      = morphyn.GetBool("Player", "alive");
double speed    = morphyn.GetDouble("Player", "speed");
string nickname = morphyn.GetString("Player", "nickname");

// Get field — generic (auto-converts to T)
float hp = morphyn.Get<float>("Player", "hp");
bool alive = morphyn.Get<bool>("Player", "alive");

// Get field — dynamic (returns whatever is stored: double, bool, string, MorphynPool or null)
object val = morphyn.Get("Player", "hp");

// Get raw MorphynValue
MorphynValue raw = morphyn.GetField("Player", "hp");

// Set field — accepts bool, float, double, string or MorphynValue directly
morphyn.SetField("Player", "hp", 50.0);
morphyn.SetField("Player", "alive", true);
morphyn.SetField("Player", "name", "Hero");

// Get all fields
Dictionary<string, MorphynValue> fields = morphyn.GetAllFields("Player");

// Trigger event
morphyn.Emit("Player", "damage", 25);

// Trigger event and get return value
MorphynValue result = morphyn.EmitSync("Player", "calculate", 25.0, 10.0);
float value = morphyn.Get<float>("Player", "output");

// Subscribe Morphyn entity to another entity's event
morphyn.Subscribe("Logger", "Player", "death", "onPlayerDeath");

// Unsubscribe
morphyn.Unsubscribe("Logger", "Player", "death", "onPlayerDeath");

// Subscribe C# method to a Morphyn event
morphyn.On("Player", "death", args => Debug.Log("Player died!"));

// Unsubscribe C# method
morphyn.Off("Player", "death", myHandler);

// Save / Load
morphyn.SaveState();
morphyn.LoadState("Player");
```

---

## C# Listeners

Subscribe any C# method directly to a Morphyn entity event using `On` and `Off`.

### On

```cs
MorphynController.Instance.On(entityName, eventName, handler);
```

The handler receives the same arguments the event was fired with as `object?[]`.

**Example:**

```cs
void Start()
{
    MorphynController.Instance.On("Player", "death", OnPlayerDeath);
    MorphynController.Instance.On("Player", "levelUp", args => {
        double level = Convert.ToDouble(args[0]);
        levelUpScreen.Show((int)level);
    });
}

void OnPlayerDeath(object?[] args)
{
    deathScreen.SetActive(true);
    respawnButton.interactable = true;
}

void OnDestroy()
{
    // Always unsubscribe to avoid memory leaks
    MorphynController.Instance.Off("Player", "death", OnPlayerDeath);
}
```

### Off

```cs
MorphynController.Instance.Off(entityName, eventName, handler);
```

!!! note
    Always call `Off` in `OnDestroy` to avoid memory leaks and calls on destroyed objects.

### Difference from Subscribe

| | `On` / `Off` | `Subscribe` / `Unsubscribe` |
|---|---|---|
| Subscriber | C# `Action<object?[]>` | Morphyn entity event |
| Use case | Unity UI, audio, effects | Morphyn-to-Morphyn logic |
| Handler | Any C# lambda or method | Morphyn event name |

```cs
// C# reacts to Morphyn event
morphyn.On("Enemy", "death", args => {
    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
});

// Morphyn entity reacts to Morphyn event
morphyn.Subscribe("Logger", "Enemy", "death", "onEnemyDeath");
```

---

## Event Subscriptions from C#

You can manage Morphyn entity subscriptions directly from C# using `Subscribe` and `Unsubscribe`.

### Subscribe

```cs
// subscriberEntity will receive handlerEvent when targetEntity fires targetEvent
morphyn.Subscribe(subscriberEntityName, targetEntityName, targetEvent, handlerEvent);
```

**Example — Logger reacts to Player death:**

```cs
MorphynController.Instance.Subscribe("Logger", "Player", "death", "onPlayerDeath");
```

This is equivalent to writing inside a `.morph` file:

```morphyn
when Player.death : onPlayerDeath
```

### Unsubscribe

```cs
morphyn.Unsubscribe(subscriberEntityName, targetEntityName, targetEvent, handlerEvent);
```

**Example:**

```cs
MorphynController.Instance.Unsubscribe("Logger", "Player", "death", "onPlayerDeath");
```

### Full Example

**logger.morph:**

```morphyn
entity Logger {
  has count: 0

  event onPlayerDeath {
    count + 1 -> count
    emit unity("Log", "Player died! Total deaths:", count)
  }

  event onPlayerLevelUp(level) {
    emit unity("Log", "Player reached level:", level)
  }
}
```

**GameManager.cs:**

```cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        var morphyn = MorphynController.Instance;
        morphyn.Subscribe("Logger", "Player", "death", "onPlayerDeath");
        morphyn.Subscribe("Logger", "Player", "levelUp", "onPlayerLevelUp");
    }

    public void OnBossFightEnd()
    {
        MorphynController.Instance.Unsubscribe("Logger", "Player", "death", "onPlayerDeath");
    }
}
```

### Notes

- Subscriptions set from C# behave exactly like `when`/`unwhen` inside `.morph` files
- An entity cannot subscribe to its own instance — `Subscribe("Player", "Player", ...)` will log a warning and do nothing
- Duplicate subscriptions are ignored
- Dead entities are cleaned up automatically during garbage collection

---

## Unity Bridge

Call Unity functions from `.morph` files.

### Setup

```cs
using UnityEngine;
using Morphyn.Unity;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Register callbacks BEFORE MorphynController.Start()
        UnityBridge.Instance.RegisterCallback("PlaySound", args => {
            string soundName = args[0]?.ToString() ?? "";
            AudioSource.PlayOneShot(Resources.Load<AudioClip>(soundName));
        });

        UnityBridge.Instance.RegisterCallback("ShowMessage", args => {
            Debug.Log($"Message: {string.Join(" ", args)}");
        });
    }
}
```

### Calling from Morphyn

```morphyn
entity Enemy {
  has hp: 50

  event damage(amount) {
    hp - amount -> hp
    emit unity("PlaySound", "hit")
    check hp <= 0: {
      emit unity("PlaySound", "explosion")
      emit unity("ShowMessage", "Enemy destroyed!")
    }
  }
}
```

### Built-in Callbacks

MorphynController registers these automatically:

```morphyn
emit unity("Log", "Hello", "World")   # Debug.Log
emit unity("Move", 1, 0, 0)           # moves MorphynController's transform
emit unity("Rotate", 45)              # rotates MorphynController's transform
```

---

## Hot Reload

Edit `.morph` files during Play mode — changes apply instantly.

**Enable:** Check `Enable Hot Reload` on MorphynController.

1. Start Play mode
2. Open `player.morph`
3. Change `has damage: 10` to `has damage: 999`
4. Save
5. Damage updates immediately — no restart needed

---

## Save / Load

Morphyn saves entity state as readable `.morph` files.

```cs
// Save all entities
MorphynController.Instance.SaveState();
// Saved to: Application.persistentDataPath/MorphynData/

// Load single entity
MorphynController.Instance.LoadState("Player");

// Auto-save on quit — set in Inspector
```

**Saved file example:**

```morphyn
entity Player {
  has hp: 75
  has level: 5
  has gold: 230
}
```

---

## Best Practices

### 1. One MorphynController per scene

```cs
MorphynController.Instance // singleton — only one instance exists
```

### 2. Register callbacks early

```cs
[DefaultExecutionOrder(-100)]
public class Setup : MonoBehaviour
{
    void Awake() // Use Awake, not Start
    {
        UnityBridge.Instance.RegisterCallback("MyCallback", args => {});
    }
}
```

### 3. Use for configs and logic, not complex gameplay systems

```cs
// GOOD
entity ShopPrices { has swordCost: 100 }

// BAD
entity ComplexAI { /* keep heavy simulation in C# */ }
```

---

## Troubleshooting

### "MorphynController not found"

```cs
if (MorphynController.Instance == null)
    Debug.LogError("Add MorphynController to scene!");
```

### "Callback not found"

- Register callbacks in `Awake()`, not `Start()`
- Register **before** MorphynController loads scripts

### Hot reload not working

- Editor only — not available in builds
- File must be inside the `Assets` folder
- Check `Enable Hot Reload` is ticked

### Values not updating

```cs
morphyn.SetField("Player", "hp", 50.0);
float hp = morphyn.GetFloat("Player", "hp"); // 50
```

### Subscribe warning: entity not found

```cs
// MorphynController.Start() calls LoadAndRun() automatically if runOnStart is true
// Safe to subscribe from Start() or later
void Start()
{
    MorphynController.Instance.Subscribe("Logger", "Player", "death", "onPlayerDeath");
}
```

### C# listener called after object destroyed

```cs
void OnDestroy()
{
    MorphynController.Instance.Off("Player", "death", myHandler);
}
```