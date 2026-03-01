<div align="center">
<img src="https://github.com/user-attachments/assets/c5d54834-7e49-4a55-a0b9-91d12442d12a" width="128" height="128" alt="Morphyn Logo" />
    
# Morphyn 
    
### [Morphyn](https://jvnkoo.github.io/morphyn) is a scripting language providing a clean, event-driven syntax to manage configs and logic. It's designed to be lightweight and opinionated, focusing entirely on entity states and reactive events without the overhead of a general-purpose language.

[📥 Download](https://github.com/jvnkoo/morphyn/releases/latest) · [📖 Docs](https://jvnkoo.github.io/morphyn) · [💡 Examples](https://jvnkoo.github.io/morphyn/examples/basic/) · [🐛 Issues](https://github.com/jvnkoo/morphyn/issues)

![GitHub stars](https://img.shields.io/github/stars/jvnkoo/morphyn?style=social)
![License](https://img.shields.io/badge/license-Apache%202.0-blue)
![Unity](https://img.shields.io/badge/Unity-2020.3+-black)
![Status](https://img.shields.io/badge/status-beta-orange)
</div>

---
## Here's a taste:
```coffeescript
entity MathUtils {
    event get_crit_chance(dex) {
        dex * 0.5 -> chance
        check chance > 50: 50 -> chance
        chance -> result
    }
    event calculate_damage(raw_dmg, armor) {
        raw_dmg * (100 / (100 + armor)) -> final_dmg
        final_dmg -> result
    }
}
```

---
## Quick Start

**1.** Download and import [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2.** Create a `.morph` file:
```coffeescript
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

Full docs at [jvnkoo.github.io/morphyn](https://jvnkoo.github.io/morphyn).

---
## Why not Lua?

The Unity bridges are a mess. MoonSharp hasn't been updated in years.
XLua is maintained but built for a different ecosystem entirely.
Getting either to work with hot reload and state preservation is a project in itself.

Morphyn exists because setting up Lua in Unity shouldn't take days.
Simpler, opinionated, built specifically for game config and logic.
You lose the standard library. You gain something that works on the first try.

---
## VS Code Extension

Syntax highlighting, bracket matching, comment support for `.morph` files.

[📥 Download `.vsix` from Releases](https://github.com/jvnkoo/morphyn/releases/latest) → Extensions → `...` → Install from VSIX

---
## Standalone Runtime

Works with any .NET project, no Unity required.

```bash
# Linux / macOS
./install.sh && morphyn game.morph

# Windows
.\install.ps1; morphyn game.morph
```

---
## Roadmap

- [x] Core language runtime
- [x] Unity integration  
- [x] Hot reload system
- [x] VS Code extension
- [ ] Async event handling
- [ ] More documentation examples
- [ ] Performance optimizations
- [ ] Self-hosted interpreter

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
