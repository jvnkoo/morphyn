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
  on init {
    emit log("Enemy spawned")
  }
}
```
### tick(dt)
Called every frame with delta time in milliseconds:
```morphyn
entity Timer {
  has time: 0
  on tick(dt) {
    time + dt -> time
  }
}
```
### destroy
Marks entity for garbage collection:
```morphyn
entity Enemy {
  has hp: 50
  on damage(v) {
    hp - v -> hp
    check hp <= 0: emit self.destroy
  }
}
```
## Custom Events
Define your own events:
```morphyn
entity Player {
  on jump {
    emit log("Player jumped!")
  }
  on heal(amount) {
    hp + amount -> hp
  }
}
```
## Sending Events
### Send to Self
```morphyn
emit event_name 

# or

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
unwhen TargetEntity.eventName : handlerEvent
```

When `TargetEntity.eventName` fires, the runtime automatically sends `handlerEvent` to the subscribing entity, passing the same arguments.

### Basic Example
```morphyn
entity Logger {
  on init {
    when Player.death : onPlayerDeath
    when Player.levelUp : onPlayerLevelUp
  }

  on onPlayerDeath {
    emit log("Player has died!")
  }

  on onPlayerLevelUp(level) {
    emit log("Player reached level:", level)
  }
}

entity Player {
  has hp: 100
  has level: 1

  on death {
    emit log("Player died!")
  }

  on levelUp(level) {
    emit log("Level up:", level)
  }
}
```

### Unsubscribing
Use `unwhen` to stop receiving events:
```morphyn
entity Logger {
  on init {
    when Player.death : onPlayerDeath
  }

  on onPlayerDeath {
    emit log("Player died — unsubscribing now")
    unwhen Player.death : onPlayerDeath
  }
}
```

### Arguments
The subscriber's handler receives the same arguments as the original event:
```morphyn
entity UI {
  on init {
    when Player.takeDamage : onDamage
  }

  on onDamage(amount) {
    emit log("UI: player took", amount, "damage")
  }
}

entity Player {
  on takeDamage(amount) {
    hp - amount -> hp
  }
}
```

If the handler declares no parameters, it simply ignores the arguments — no error:
```morphyn
on onDamage {
  emit log("Player was hit")
}
```

### Rules
- **An entity cannot subscribe to its own instance's events.** A specific entity instance cannot listen to events it fires itself — the subscriber and the target must be different instances:
```morphyn
entity Logger {
  on init {
    when Logger.something : onSomething  # runtime error — same instance subscribing to itself
    when Player.death : onPlayerDeath    # ok — different entity
  }
}
```
Two different clones of the same entity type (e.g. spawned via `pool.add`) are separate instances and can subscribe to each other normally.
- **Duplicate subscriptions are ignored.** Subscribing the same handler to the same event twice has no effect.
- **Dead entities are cleaned up automatically.** If a subscriber is destroyed, its subscriptions are removed during garbage collection.
- **`when` and `unwhen` can be used anywhere** — inside `on init`, `on tick`, or any other event handler.

### Multiple Subscribers
Multiple entities can subscribe to the same event independently:
```morphyn
entity Logger {
  on init {
    when Player.death : onPlayerDeath
  }
  on onPlayerDeath {
    emit log("Logger: player died")
  }
}

entity UI {
  on init {
    when Player.death : onPlayerDeath
  }
  on onPlayerDeath {
    emit log("UI: showing death screen")
  }
}

entity Stats {
  on init {
    when Player.death : recordDeath
  }
  on recordDeath {
    deaths + 1 -> deaths
  }
}
```

All three will react when `Player.death` fires.

## Built-in Functions
Built-in functions are called via `emit` but handled directly by the runtime.
### log
Prints values to console:
```morphyn
emit log("HP:", hp)
emit log("Position:", x, y)
emit log("Pool:", items)
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
!!! note
    The field name must be passed as a string literal in quotes: `"fieldName"`.
    Writing `emit input("prompt", fieldName)` will not work — `fieldName` would
    be evaluated as a variable, not as a field name.
**Example — interactive calculator:**
```morphyn
entity Calc {
  has a: 0
  has b: 0
  has op: ""
  on init {
    emit input("First number: ", "a")
    emit input("Operator (+,-,*,/): ", "op")
    emit input("Second number: ", "b")
    emit calculate
  }
  on calculate {
    check op == "+": emit log(a, "+", b, "=", a + b)
    check op == "-": emit log(a, "-", b, "=", a - b)
    check op == "*": emit log(a, "*", b, "=", a * b)
    check op == "/": {
      check b == 0: emit log("error: division by zero")
      check b != 0: emit log(a, "/", b, "=", a / b)
    }
  }
}
```
### unity
Calls a registered Unity callback:
```morphyn
emit unity("PlaySound", "explosion")
emit unity("SpawnEnemy", x, y)
```