# Morphyn Documentation

## About
 [Morphyn](https://jvnkoo.github.io/morphyn) is a scripting language providing a clean, event-driven syntax to manage configs and logic. Lightweight and opinionated — focused entirely on entity states and reactive events, without the overhead of a general-purpose language.

---

## Here's a taste:
```morphyn
entity Player {
  has hp: 100
  has level: 1

  event damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit die
  }

  event level_up {
    level + 1 -> level
    hp + 20 -> hp
  }
}
```
---

## Quick Start

### Unity

**1.** Download and import [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2.** Create a `.morph` file:
```morphyn
entity Enemy {
    has hp: 100
    has alive: true

    event take_damage(amount) {
        hp - amount -> hp
        emit log("Enemy hit! HP:", hp)

        check hp <= 0: {
            false -> alive
            emit log("Enemy defeated")
        }
    }
}
```

**3.** Use in C#:
```csharp
MorphynController.Instance.Emit("Enemy", "take_damage", 25);

bool isAlive = Convert.ToBoolean(MorphynController.Instance.GetField("Enemy", "alive"));
```

**4.** Add `MorphynController` to your scene, drag in the `.morph` files, check **Enable Hot Reload**, press Play.

[Full Unity Guide →](unity/overview.md)

### Standalone

```bash
# Linux / macOS
./install.sh && morphyn game.morph

# Windows
.\install.ps1; morphyn game.morph
```

[Installation Guide →](getting-started/installation.md)

---

## Why not Lua?

The Unity bridges are a mess. MoonSharp hasn't been updated in years. XLua is maintained but built for a different ecosystem entirely. Getting either to work with hot reload and state preservation is a project in itself.

Morphyn exists because setting up Lua in Unity shouldn't take days. Simpler, opinionated, built specifically for game config and logic. You lose the standard library. You gain something that works on the first try.

---

## Core Concepts

**Entities** hold state and define reactions:
```morphyn
entity Shop {
  has gold: 100
  event buy(cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit add_item("sword")
    }
  }
}
```

**Events** are how entities communicate:
```morphyn
emit Player.damage(25)   # Send to entity
emit self.heal(10)       # Send to self explicitly
emit heal(10)            # Same as above, implicit
```

**Subscriptions** let entities react to each other:
```morphyn
entity Logger {
  event init {
    when Player.die : onPlayerDied
  }
  event onPlayerDied {
    emit log("Player died")
  }
}
```

**Data flow** with `->`:
```morphyn
hp - 10 -> hp       # Subtract
max_hp -> hp        # Set
```

**Check** stops execution if condition fails (only if there is no block to execute):
```morphyn
check false
```

---

## VS Code Extension

Syntax highlighting, bracket matching, comment support for `.morph` files.

[📥 Download `.vsix` from Releases](https://github.com/jvnkoo/morphyn/releases/latest) → Extensions → `...` → Install from VSIX

---

## Roadmap

- [x] Core language runtime
- [x] Unity integration
- [x] Hot reload system
- [x] VS Code extension
- [x] Event subscription system (`when` / `unwhen`)
- [x] C# listener API (`On` / `Off`)
- [ ] Async event handling
- [ ] More documentation examples
- [ ] Performance optimizations
- [ ] Self-hosted interpreter

---

## Learn More

**Getting Started:** [Quick Start](getting-started/quick-start.md) · [Installation](getting-started/installation.md)

**Language:** [Syntax Reference](language/syntax.md) · [Event System](language/events.md) · [Expression System](language/expressions.md)

**Unity:** [Overview](unity/overview.md) · [API Reference](unity/api.md)

**Learn in Y minutes:** [Morphyn](learn/learn.md) · [Unity bridge](learn/learn-unity.md)

---

## Contributing

- [Report Issues](https://github.com/jvnkoo/morphyn/issues)
- [Feature Requests](https://github.com/jvnkoo/morphyn/issues) (use "enhancement" label)

PRs are welcome.

---

## License

Apache 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE). Free for commercial use.

---

<div align="center">
<img src="https://media1.tenor.com/m/ugRQCY7AKEsAAAAd/texh-texhnolyze.gif" width="1000" height="300" alt="gif">
</div>