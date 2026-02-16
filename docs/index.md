# Morphyn Documentation

## What is Morphyn?

**Config files with built-in logic and instant hot reload.**

### The Problem

You're balancing your game. You change enemy damage from `10` to `15`.  
You stop Play mode. Wait for Unity to reload. Press Play. Test. Too weak.  
Change to `20`. Stop. Wait. Play. Test again.

**Repeat 50 times a day = 10+ minutes wasted.**

### The Solution
```morphyn
entity Enemy {
  has damage: 10  # ← Change this to 999
}
```

Save. **Game updates instantly. While running.**

---

## Quick Example
```morphyn
entity Player {
  has hp: 100
  has level: 1
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit die
  }
  
  on level_up {
    level + 1 -> level
    hp + 20 -> hp
  }
}
```

**That's it.** No classes, no inheritance, no boilerplate.

---

## Getting Started

### For Unity Developers

**1. Install**  
Download [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2. Create config**
```morphyn
entity GameSettings {
  has difficulty: "normal"
  has enemy_damage: 10
}
```

**3. Use in C#**
```csharp
double damage = System.Convert.ToDouble(
    MorphynController.Instance.GetField("GameSettings", "enemy_damage")
);
```

**4. Edit while playing**  
Change `enemy_damage: 10` → `enemy_damage: 999` and save.  
**Value updates instantly.**

[Full Unity Guide →](unity/overview.md)

### For Standalone Use

**1. Download** runtime from [Releases](https://github.com/jvnkoo/morphyn/releases/latest)

**2. Install** (adds `morphyn` command to PATH)
```bash
./install.sh  # Linux/macOS
.\install.ps1  # Windows
```

**3. Run**
```bash
morphyn game.morphyn
```

[Installation Guide →](getting-started/installation.md)

---

## Why Use Morphyn?

### ⚡ Hot Reload Everything
Not just values — **logic too**.
```morphyn
on damage(amount) {
  hp - amount -> hp
  check hp <= 0: emit die  # ← Change condition while game runs
}
```

### 🛡️ Built-in Validation
```morphyn
on heal(amount) {
  check amount > 0: hp + amount -> hp  # Auto-validates
  check hp > max_hp: max_hp -> hp      # Auto-clamps
}
```

### 🎯 Made for Game Logic
```morphyn
entity Shop {
  has gold: 100
  
  on buy(cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit add_item("sword")
    }
  }
}
```

---

## Core Concepts

### Entities
Entities are like objects with state and behavior.
```morphyn
entity Player {
  has hp: 100        # State
  on damage(amount) { # Behavior
    hp - amount -> hp
  }
}
```

### Events
Events trigger entity reactions.
```morphyn
emit player.damage(25)  # Send event to player
emit heal(10)           # Send to self
```

### Data Flow
Use `->` to update values.
```morphyn
hp - 10 -> hp          # Subtract 10 from hp
max_hp -> hp           # Set hp to max_hp
```

### Checks
Guards that stop execution if condition fails.
```morphyn
check hp > 0: emit alive       # Only if hp > 0
check gold >= cost: emit buy   # Only if enough gold
```

---

## Learn More

**Getting Started:**
- [Quick Start](getting-started/quick-start.md)
- [Installation](getting-started/installation.md)

**Language:**
- [Syntax Reference](language/syntax.md)
- [Event System](language/events.md)
- [Expression System](language/expressions.md)

**Unity:**
- [Unity Integration](unity/overview.md)
- [Unity API](unity/api.md)
- [Unity Examples](unity/examples.md)

**Examples:**
- [Basic Examples](examples/basic.md)
- [Game Examples](examples/game.md)