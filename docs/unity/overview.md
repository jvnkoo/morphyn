# Unity Integration

## What is Morphyn for Unity?

**Config files with built-in logic and instant hot reload.**

Stop restarting Unity every time you change a game balance value.

## The Problem

Traditional Unity workflow:
1. Change enemy HP in C# script
2. Stop Play mode
3. Wait for Unity to reload
4. Press Play
5. Test
6. Repeat 50x per day

**Result: 10+ minutes wasted on context switching.**

## The Solution

With Morphyn:
1. Change enemy HP in `.morph` file
2. Save
3. **Game updates instantly while running**

!!! note
    Unity will still recompile in the background when you save .morph files, but your game keeps running and state is preserved.

## Comparison

### ❌ ScriptableObject
```cs
[CreateAssetMenu]
public class EnemyData : ScriptableObject
{
    public int hp = 100;
    public int damage = 25;
}
```

**Change value → Must stop Play mode**

---

### ✅ Morphyn
```morphyn
entity Enemy {
  has hp: 100
  has damage: 25
}
```

**Change value → Updates while playing**

## When to Use Morphyn

### ✅ Perfect For
- Game balance (HP, damage, XP curves)
- Shop systems (prices, stock)
- Quest data (requirements, rewards)
- AI parameters (aggro range, patrol speed)
- Difficulty settings

### ⚠️ Not Recommended For
- Complex algorithms (use C#)
- Performance-critical loops (use C#)
- UI rendering (use Unity UI)

**Rule of thumb:** If you'd use a ScriptableObject, use Morphyn instead.

## Key Features

### Hot Reload
Edit logic and values in Play mode without restarting.
```morphyn
on damage(amount) {
  hp - amount -> hp
  check hp <= 0: emit die  # ← Change this while game runs
}
```

### Built-in Validation
No more manual null checks or validation code.
```morphyn
on heal(amount) {
  check amount > 0: hp + amount -> hp  # Auto-validates
  check hp > max_hp: max_hp -> hp      # Auto-clamps
}
```

### Event-Driven Logic
Reactive behaviors without complex state machines.
```morphyn
entity Shop {
  has gold: 100
  
  on buy_item(cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit inventory.add("sword")
    }
    check gold < cost: {
      emit unity("ShowError", "Not enough gold")
    }
  }
}
```

## Next Steps

- [Installation Guide](installation.md)
- [API Reference](api.md)
- [Code Examples](examples.md)