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
  on clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }

  on abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }

  on lerp(a, b, t) {
    a + (b - a) * t -> result
  }
}

entity Player {
  has hp: 150
  has max_hp: 100
  has x: 0.0

  on heal(amount) {
    emit MathLib.clamp(hp + amount, 0, max_hp) -> hp
  }

  on tick(dt) {
    emit MathLib.lerp(x, 10, 0.1) -> x
  }
}
```

---

## Rules

### Nested sync calls are forbidden

Using `emit X() -> field` inside a sync event throws a runtime error. This prevents recursion entirely.

```morphyn
entity Bad {
  on recursive(value) {
    emit Bad.recursive(value) -> value  # runtime error — nested sync call
  }
}
```

### Regular emit is allowed inside sync events

Regular `emit` just queues the event as usual — no recursion risk.

```morphyn
entity MathLib {
  on clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
    emit log("clamped:", result)  # ok — queued normally
  }
}
```

### Return value

The return value is the **last value assigned** inside the event body, regardless of which branch executed.

```morphyn
on clamp(value, min, max) {
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
| `emit X() -> field` allowed inside | Yes | No |
| Regular `emit` allowed inside | Yes | Yes |
| Recursion possible | Yes (indirect) | No |
| Use case | Events, side effects | Pure computation |

---

## Use Cases

### Math utilities

```morphyn
entity MathLib {
  on clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }

  on abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }

  on lerp(a, b, t) {
    a + (b - a) * t -> result
  }

  on normalize(value, min, max) {
    (value - min) / (max - min) -> result
  }
}
```

### Stat computation

```morphyn
entity StatLib {
  on apply_armor(damage, armor) {
    damage * (1 - armor / 100) -> result
  }

  on exp_to_next(level) {
    level * level * 50 -> result
  }
}

entity Player {
  has hp: 100
  has armor: 30
  has level: 5
  has exp: 0

  on take_damage(amount) {
    emit StatLib.apply_armor(amount, armor) -> amount
    hp - amount -> hp
    check hp <= 0: emit die
  }

  on add_exp(amount) {
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
  on clamp(value, min, max) {
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

  on heal(amount) {
    emit MathLib.clamp(hp + amount, 0, max_hp) -> hp
  }
}
```