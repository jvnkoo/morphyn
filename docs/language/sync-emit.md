# Sync Emit
## Overview
Sync emit is a special form of `emit` that executes an event **immediately and synchronously**, bypassing the event queue, and assigns the result to a field.
```morphyn
emit Entity.event(args) -> field
```
This enables user-defined pure functions — events that compute and return a value.

---
## Syntax
```morphyn
emit EventName(args) -> field
emit Entity.event(args) -> field
```
The result written to `field` is the **last assigned value** inside the called event.

---
## Example
```morphyn
entity MathLib {
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }
  event abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }
  event lerp(a, b, t) {
    a + (b - a) * t -> result
  }
}
entity Player {
  has hp: 150
  has max_hp: 100
  has x: 0.0
  event heal(amount) {
    emit MathLib.clamp(hp + amount, 0, max_hp) -> hp
  }
  event tick(dt) {
    emit MathLib.lerp(x, 10, 0.1) -> x
  }
}
```

---
## Rules

### Sync calls can be chained
`emit X() -> field` can be used inside another sync event, as long as the target is a **different** event. Chains like `A -> B -> C` are fully supported:

```morphyn
entity Pipeline {
  event process(value) {
    emit MathLib.abs(value) -> value            # ok — calls MathLib.abs
    emit MathLib.clamp(value, 0, 100) -> value  # ok — calls MathLib.clamp
    value -> result
  }
}

entity MathLib {
  event abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }
}
```

### Direct recursion is forbidden
An event **cannot call itself** synchronously. This prevents infinite loops:

```morphyn
entity Bad {
  event recurse(value) {
    emit Bad.recurse(value) -> value  # runtime error — recursive sync call
  }
}
```

The check is per `(entity, event)` pair — `A.foo` calling `A.foo` is forbidden, but `A.foo` calling `A.bar` or `B.foo` is fine.

### Regular emit inside sync events
Regular `emit` is allowed inside sync events. The events are held in a separate side-effect queue and flushed to the main queue **after the outermost sync call completes**. This means side effects happen after the result is returned, not during.

```morphyn
entity MathLib {
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
    emit log("clamped:", result)  # queued, fires after sync call returns
  }
}
```

### Return value
The return value is the **last value assigned** inside the event body, regardless of which branch executed.
```morphyn
event clamp(value, min, max) {
  check value < min: min -> value
  check value > max: max -> value
  value -> result             # this is the return value
}
```

---
## Comparison with regular emit
| | `emit X()` | `emit X() -> field` |
|---|---|---|
| Execution | Queued, deferred | Immediate, synchronous |
| Return value | None | Last assigned value |
| `emit X() -> field` allowed inside | Yes | Yes, unless recursive |
| Regular `emit` allowed inside | Yes | Yes (deferred to after sync) |
| Recursion | Yes (indirect) | No (runtime error) |
| Chaining to other events | — | Yes |
| Use case | Events, side effects | Pure computation, pipelines |

---
## Use Cases

### Math utilities
```morphyn
entity MathLib {
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }
  event abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }
  event lerp(a, b, t) {
    a + (b - a) * t -> result
  }
  event normalize(value, min, max) {
    (value - min) / (max - min) -> result
  }
}
```

### Chained computation
```morphyn
entity StatLib {
  event raw_to_final(damage, armor, crit) {
    emit StatLib.apply_armor(damage, armor) -> damage
    emit StatLib.apply_crit(damage, crit) -> damage
    damage -> result
  }
  event apply_armor(damage, armor) {
    damage * (1 - armor / 100) -> result
  }
  event apply_crit(damage, crit) {
    check crit: damage * 2 -> damage
    damage -> result
  }
}
entity Player {
  has hp: 100
  has armor: 30
  event take_damage(raw, crit) {
    emit StatLib.raw_to_final(raw, armor, crit) -> final
    hp - final -> hp
    check hp <= 0: emit die
  }
}
```

### Stat computation
```morphyn
entity StatLib {
  event apply_armor(damage, armor) {
    damage * (1 - armor / 100) -> result
  }
  event exp_to_next(level) {
    level * level * 50 -> result
  }
}
entity Player {
  has hp: 100
  has armor: 30
  has level: 5
  has exp: 0
  event take_damage(amount) {
    emit StatLib.apply_armor(amount, armor) -> amount
    hp - amount -> hp
    check hp <= 0: emit die
  }
  event add_exp(amount) {
    exp + amount -> exp
    emit StatLib.exp_to_next(level) -> needed
    check exp >= needed: emit level_up
  }
}
```

### Shared library via import
**mathlib.morph:**
```morphyn
entity MathLib {
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }
}
```
**game.morph:**
```morphyn
import "mathlib.morph"
entity Player {
  has hp: 100
  has max_hp: 100
  event heal(amount) {
    emit MathLib.clamp(hp + amount, 0, max_hp) -> hp
  }
}
```