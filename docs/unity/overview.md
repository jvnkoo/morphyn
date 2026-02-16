# Unity Integration

## Overview

**Morphyn = JSON + Logic**

Stop using dumb JSON configs. Morphyn is a smart configuration language that runs logic when values change.

## Why Morphyn > JSON

| JSON | Morphyn |
|------|---------|
| Only stores data | Stores data + behavior |
| Needs external code for validation | Built-in checks |
| Can't react to changes | Event-driven reactions |
| Static values | Dynamic calculations |
| No hot reload | Edit configs in Play mode |

## Think of it as:

- Config files that validate themselves
- PlayerPrefs with built-in logic
- ScriptableObjects without C# code
- Smart data containers

## JSON vs Morphyn Comparison

### The JSON Way (BAD)

**player_config.json:**
```json
{
  "hp": 100,
  "maxHp": 100,
  "level": 1,
  "exp": 0
}
```

**PlayerController.cs:**
```cs
public class PlayerController : MonoBehaviour
{
    private PlayerConfig config;
    
    void Start() {
        string json = File.ReadAllText("player_config.json");
        config = JsonUtility.FromJson<PlayerConfig>(json);
    }
    
    public void AddExp(int amount) {
        config.exp += amount;
        
        // Manual logic every time
        if (config.exp >= 100) {
            config.level++;
            config.exp = 0;
            config.maxHp += 20;
            config.hp = config.maxHp;
            Debug.Log("LEVEL UP!");
        }
    }
    
    public void Damage(int amount) {
        config.hp -= amount;
        
        // More manual logic
        if (config.hp <= 0) {
            Debug.Log("GAME OVER");
        }
    }
}
```

### The Morphyn Way (GOOD)

**player.morphyn:**
```morphyn
entity Player {
  has hp: 100
  has maxHp: 100
  has level: 1
  has exp: 0
  
  on add_exp(amount) {
    exp + amount -> exp
    
    check exp >= 100: {
      level + 1 -> level
      0 -> exp
      maxHp + 20 -> maxHp
      maxHp -> hp
      emit unity("Log", "LEVEL UP! Now level", level)
    }
  }
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit unity("Log", "GAME OVER")
  }
}
```

**PlayerController.cs:**
```cs
public class PlayerController : MonoBehaviour
{
    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            MorphynController.Instance.SendEventToEntity("Player", "add_exp", 35);
        }
        
        if (Input.GetKeyDown(KeyCode.D)) {
            MorphynController.Instance.SendEventToEntity("Player", "damage", 20);
        }
    }
}
```

**Result:** Config file handles ALL logic. No C# boilerplate.