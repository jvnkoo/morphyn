# Event System

## Overview

Morphyn uses an event queue to process entity reactions. Events are processed
in order, and each event can trigger additional events.

## Built-in Events

### init
Called when an entity is first created or spawned:
```morphyn
entity Enemy {
  has hp: 50
  event init {
    emit log("Enemy spawned")
  }
}
```

### tick(dt)
Called every frame with delta time in milliseconds:
```morphyn
entity Timer {
  has time: 0
  event tick(dt) {
    time + dt -> time
  }
}
```

### destroy
Marks entity for garbage collection:
```morphyn
entity Enemy {
  has hp: 50
  event damage(v) {
    hp - v -> hp
    check hp <= 0: emit self.destroy
  }
}
```

## Custom Events
Define your own events:
```morphyn
entity Player {
  event jump {
    emit log("Player jumped!")
  }
  event heal(amount) {
    hp + amount -> hp
  }
}
```

## Sending Events

### Send to Self
```morphyn
emit event_name
emit self.event_name
emit heal(10)
```

### Send to Target
```morphyn
emit target.event_name
emit player.damage(5)
```

## Event Subscriptions

Entities can subscribe to events of other entities using `when` and unsubscribe using `unwhen`.

### Syntax
```morphyn
when TargetEntity.eventName : handlerEvent
when TargetEntity.eventName : handlerEvent(arg)

unwhen TargetEntity.eventName : handlerEvent
unwhen TargetEntity.eventName : handlerEvent(arg)
```

When `TargetEntity.eventName` fires, the runtime sends `handlerEvent` to the subscribing entity with the arguments defined in the `when` statement — **not** the arguments from the original event.

The argument in `handlerEvent(arg)` is evaluated against the **subscriber** entity at the moment the event fires. This means if `arg` is a field name, its current value is read from the subscriber at that point in time.

### Basic Example — no args
```morphyn
entity Logger {
  event init {
    when Player.death : onPlayerDeath
  }

  event onPlayerDeath {
    emit log("Player has died!")
  }
}
```

### With a fixed argument
```morphyn
entity Logger {
  event init {
    when Player.death : onPlayerDeath(42)  # always passes 42
  }

  event onPlayerDeath(code) {
    emit log("Player died. Code:", code)
  }
}
```

### With a field argument — read at fire time
```morphyn
entity Logger {
  has severity: 3

  event init {
    when Player.death : onPlayerDeath(severity)  # reads Logger.severity when Player.death fires
  }

  event onPlayerDeath(sev) {
    emit log("Player died. Severity:", sev)
  }
}
```

### Unsubscribing
```morphyn
entity Logger {
  has severity: 3

  event init {
    when Player.death : onPlayerDeath(severity)
  }

  event onPlayerDeath(sev) {
    emit log("Player died")
    unwhen Player.death : onPlayerDeath(severity)  # matches the original when
  }
}
```

### Rules
- An entity cannot subscribe to its own instance's events.
- Duplicate subscriptions are ignored.
- Destroyed entities are cleaned up automatically.
- `when` and `unwhen` can be used in any event, not just `init`.
- `unwhen` args must match the args used in the original `when`.

---

## Field Change Subscriptions

Entities can also subscribe to **field value changes** using `watch` and unsubscribe using `unwatch`.

The handler receives `(oldValue, newValue)` as arguments. It fires only when the value actually changes — setting a field to the same value does not trigger watchers.

### Syntax
```morphyn
watch fieldName : handlerEvent              # watch own field (self)
watch TargetEntity.fieldName : handlerEvent # watch field on another entity

unwatch fieldName : handlerEvent
unwatch TargetEntity.fieldName : handlerEvent
```

### Basic Example — watch own field
```morphyn
entity Player {
  has hp: 100

  event init {
    watch hp : onHpChanged
  }

  event onHpChanged(old, new) {
    emit log("hp changed:", old, "->", new)
    check new <= 0: emit die
  }

  event takeDamage(amount) {
    hp - amount -> hp
  }
}
```

### Watch a field on another entity
```morphyn
entity UI {
  event init {
    watch Player.hp : onPlayerHpChanged
  }

  event onPlayerHpChanged(old, new) {
    emit log("UI: player hp changed:", old, "->", new)
  }
}
```

### Unwatch
```morphyn
entity UI {
  event init {
    watch Player.hp : onPlayerHpChanged
  }

  event onPlayerHpChanged(old, new) {
    emit log("UI: player hp changed:", old, "->", new)
    unwatch Player.hp : onPlayerHpChanged  # stop watching after first change
  }
}
```

### Rules
- The handler receives exactly two arguments: `(oldValue, newValue)`.
- Fires only when the value **changes** — assigning the same value is a no-op.
- `watch` and `unwatch` can be used in any event, not just `init`.
- Duplicate watches are ignored.
- Destroyed entities are cleaned up automatically.
- An entity can watch its own fields or fields on other entities.

---

## Built-in Functions

Built-in functions are called via `emit` but handled directly by the runtime.

### log
Prints values to console:
```morphyn
emit log("HP:", hp)
emit log("Position:", x, y)
```

### input
Reads a line from console and writes the value to a field:
```morphyn
emit input("Enter your name: ", "name")
emit input("Enter amount: ", "amount")
```
- First argument: prompt string shown to user
- Second argument: **field name as a string literal** (in quotes)
- If the input can be parsed as a number, it is stored as a number
- Otherwise stored as a string

### unity
Calls a registered Unity callback:
```morphyn
emit unity("PlaySound", "explosion")
emit unity("SpawnEnemy", x, y)
```