# Quick Start

## Your First Program

Create a file called `player.morphyn`:
```morphyn
entity Player {
  has hp: 100
  has name: "Hero"
  
  on init {
    emit log("Player created!")
    emit damage(25)  # Trigger damage event from init
  }
  
  on damage(amount) {
    hp - amount -> hp
    emit log("HP remaining:", hp)
    check hp <= 0: emit die
  }
  
  on die {
    emit log("Game Over")
  }
}
```

## Running Morphyn Programs

Run your program:
```sh
morphyn player.morphyn
```

**Output:**
```
Player created!
HP remaining: 75
```

---

## How Events Work

Events in Morphyn are **reactive**, not automatic. They only run when:

1. **Triggered from `init` or `tick`:**
```morphyn
entity Game {
  on init {
    emit player.damage(50)  # Triggers player's damage event
  }
  
  on tick(dt) {
    emit enemy.update(dt)   # Triggers enemy's update event
  }
}
```

2. **Triggered externally** (from C# in Unity):
```csharp
MorphynController.Instance.SendEventToEntity("Player", "damage", 50);
```

3. **Chained from other events:**
```morphyn
on damage(amount) {
  hp - amount -> hp
  check hp <= 0: emit die  # damage triggers die
}
```

!!! important
    Custom events like `damage`, `heal`, `attack` **will not run** unless explicitly triggered. Only `init` and `tick` run automatically.

---

## Runtime Features

### Hot Reload

The runtime automatically watches for file changes and reloads entity logic without restarting.

**Example:**
1. Run `morphyn player.morphyn`
2. Edit the file: change `emit damage(25)` to `emit damage(75)`
3. Save
4. **Logic updates instantly** without restarting

### Tick System

Entities with a `tick` event receive frame updates:
```morphyn
entity Timer {
  has elapsed: 0
  
  on tick(dt) {
    # dt = milliseconds since last frame
    elapsed + dt -> elapsed
    emit log("Time:", elapsed)
  }
}
```

### Init Event

Runs once when entity is created:
```morphyn
entity Player {
  has hp: 100
  
  on init {
    emit log("Player spawned with", hp, "HP")
  }
}
```