# Unity API Reference

## MorphynController

The main component that loads and runs .morphyn files.

### Settings

| Setting | Description |
|---------|-------------|
| **Morphyn Scripts** | Drag .morphyn files here |
| **Run On Start** | Auto-load on Start() |
| **Enable Tick** | Send tick(dt) every frame |
| **Enable Hot Reload** | Edit files during Play mode |
| **Auto Save** | Save on quit |
| **Save Folder** | Where to save state |

### API Methods
```cs
MorphynController morphyn = MorphynController.Instance;

// Get value
object hp = morphyn.GetField("Player", "hp");
double hpValue = System.Convert.ToDouble(hp);

// Set value
morphyn.SetField("Player", "hp", 50);

// Trigger event
morphyn.Emit("Player", "damage", 25);

// Trigger event which returns a value
object? result = morphyn.EmitSync("Player", "damage", 25);

// Save/Load
morphyn.SaveState();
morphyn.LoadState("Player");
```

## Unity Bridge

Call Unity functions from .morphyn files.

### Setup
```cs
using UnityEngine;
using Morphyn.Unity;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Register callback BEFORE MorphynController.Start()
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
  
  on damage(amount) {
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

MorphynController includes these by default:
```morphyn
emit unity("Log", "Hello", "World")     # Debug.Log
emit unity("Move", 1, 0, 0)             # Move controller transform
emit unity("Rotate", 45)                # Rotate controller
```

## Hot Reload

Edit .morphyn files during Play mode - changes apply instantly.

**Enable:** Check `Enable Hot Reload` on MorphynController

**Example:**
1. Start Play mode
2. Open player.morphyn
3. Change `has damage: 10` to `has damage: 999`
4. Save file
5. Damage updates immediately - no restart needed

## Save/Load

Morphyn saves entity state as readable .morphyn files.
```cs
// Save all entities
MorphynController.Instance.SaveState();
// Saved to: Application.persistentDataPath/MorphynData/

// Load single entity
MorphynController.Instance.LoadState("Player");

// Auto-save on quit
MorphynController.Instance.autoSave = true;
```

**Saved file example:**
```morphyn
entity Player {
  has hp: 75
  has level: 5
  has gold: 230
}
```

## Best Practices

### 1. One MorphynController per scene
```cs
// Singleton - only one instance exists
MorphynController.Instance
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

### 3. Use for configs, not gameplay code
```cs
// GOOD - config with logic
entity ShopPrices { has swordCost: 100 }

// BAD - complex gameplay
entity ComplexAI { /* don't do this */ }
```

## Troubleshooting

### "MorphynController not found"
```cs
if (MorphynController.Instance == null)
{
    Debug.LogError("Add MorphynController to scene!");
}
```

### "Callback not found"

- Register callbacks in Awake(), not Start()
- Register BEFORE MorphynController loads scripts

### Hot reload not working

- Editor only (not in builds)
- File must be in Assets folder
- Check `Enable Hot Reload` is enabled

### Values not updating
```cs
// After changing values, read again
morphyn.SetField("Player", "hp", 50);
double hp = System.Convert.ToDouble(morphyn.GetField("Player", "hp")); // 50
```