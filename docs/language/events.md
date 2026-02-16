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