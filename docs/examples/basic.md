# Basic Examples

!!! note
    Custom events (like `damage`, `heal`) must be triggered either from `init`/`tick` events, or externally via C# code using `MorphynController.Emit()`. They don't run automatically.

## Simple Counter
```morphyn
entity Counter {
  has value: 0
  
  on init {
    emit log("Counter initialized")
  }
  
  on tick(dt) {
    value + 1 -> value
    emit log("Counter:", value)
  }
}
```

**What happens:**
- `init` runs once when entity is created
- `tick` runs every frame, incrementing counter

---

## Health System
```morphyn
entity Player {
  has hp: 100
  has max_hp: 100
  has alive: true
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: {
      0 -> hp
      false -> alive
      emit die
    }
  }
  
  on heal(amount) {
    check alive: {
      hp + amount -> hp
      check hp > max_hp: max_hp -> hp
    }
  }
  
  on die {
    emit log("Player died")
  }
}
```

**How to use:**

In standalone runtime:
```morphyn
entity Game {
  on init {
    emit player.damage(50)  # Trigger damage event
    emit player.heal(20)    # Trigger heal event
  }
}
```

In Unity:
```csharp
MorphynController.Instance.Emit("Player", "damage", 50);
MorphynController.Instance.Emit("Player", "heal", 20);
```

---

## Enemy AI
```morphyn
entity Enemy {
  has hp: 50
  has damage: 10
  has state: "idle"
  
  on see_player {
    "chase" -> state
  }
  
  on tick(dt) {
    check state == "chase": emit attack
  }
  
  on attack {
    emit player.damage(damage)
  }
  
  on take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit self.destroy
  }
}
```

**How it works:**
- `see_player` event must be triggered externally (e.g., from Unity's OnTriggerEnter)
- Once in "chase" state, `tick` automatically triggers `attack` every frame
- `attack` sends damage event to player entity

**Unity example:**
```csharp
void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
    {
        MorphynController.Instance.Emit("Enemy", "see_player");
    }
}
```

---

## Inventory System
```morphyn
entity Inventory {
  has items: pool["sword", "shield"]
  has capacity: 10
  
  on init {
    emit list_items  # Show initial items
  }
  
  on add_item(item) {
    check items.count < capacity: {
      emit items.add(item)
      emit log("Added:", item)
    }
    check items.count >= capacity: {
      emit log("Inventory full!")
    }
  }
  
  on remove_item(index) {
    emit items.remove_at(index)
  }
  
  on use_item(index) {
    items.at[index] -> current_item
    emit log("Using:", current_item)
  }
  
  on list_items {
    emit log("Inventory size:", items.count)
    emit items.each(show_item)
  }
  
  on show_item(item) {
    emit log("  -", item)
  }
}
```

**How to use:**
```csharp
// Add item
MorphynController.Instance.Emit("Inventory", "add_item", "potion");

// Use item at index 1
MorphynController.Instance.Emit("Inventory", "use_item", 1);

// List all items
MorphynController.Instance.Emit("Inventory", "list_items");
```