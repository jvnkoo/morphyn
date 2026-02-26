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
emit heal(10)
```

### Send to Target

```morphyn
emit target.event_name
emit player.damage(5)
```

### Send to Self with destroy

```morphyn
emit self.destroy
```

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