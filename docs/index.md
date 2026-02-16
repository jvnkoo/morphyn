# Morphyn Language Documentation

## Introduction

Morphyn is a declarative, event-driven programming language designed for game logic and entity behavior.
Unlike traditional imperative languages, Morphyn uses data flows and reactions instead of loops and branches.

## Philosophy

- **Entity** — A world with its own laws
- **has** — The laws of that world
- **on** — Reactions to events
- **emit** — The physics of interaction
- **check** — Guards for invariants
- **->** — The flow of energy/data

## Key Features

- Event-driven architecture (no control flow)
- Hot reload support
- Real-time execution with tick-based updates
- Simple, readable syntax
- Built for game development

## Quick Example
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

## Next Steps

- [Quick Start Guide](getting-started/quick-start.md)
- [Language Syntax](language/syntax.md)
- [Unity Integration](unity/overview.md)
- [Examples](examples/basic.md)