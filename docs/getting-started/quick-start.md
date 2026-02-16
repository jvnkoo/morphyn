# Quick Start

## Your First Program

Create a file called `player.morphyn`:
```morphyn
entity Player {
  has hp: 100
  has name: "Hero"
  
  on init {
    emit log("Player created!")
  }
  
  on damage(amount) {
    hp - amount -> hp
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

## Runtime Features

### Hot Reload

The runtime automatically watches for file changes and reloads entity logic
without restarting the program or losing state.

### Tick System

Entities with a `tick` event handler receive delta time updates every frame:
```morphyn
entity Player {
  on tick(dt) {
    # dt = milliseconds since last frame
    emit log("Frame time:", dt)
  }
}
```

### Init Event

Entities with an `init` event are automatically initialized on load:
```morphyn
entity Player {
  has hp: 100

  on init {
    emit log("Player spawned with", hp, "HP")
  }
}
```