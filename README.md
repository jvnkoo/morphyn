# Morphyn — Hot Reload for Game Logic

<div align="center">
<img src="https://github.com/user-attachments/assets/c5d54834-7e49-4a55-a0b9-91d12442d12a" width="128" height="128" alt="Morphyn Logo" />

### Edit game logic while the game is running. No recompile. No restart. No lost state.

[📥 Download](https://github.com/jvnkoo/morphyn/releases/latest) · [📖 Docs](https://jvnkoo.github.io/morphyn) · [💡 Examples](https://jvnkoo.github.io/morphyn/examples/basic/) · [🐛 Issues](https://github.com/jvnkoo/morphyn/issues)

![GitHub stars](https://img.shields.io/github/stars/jvnkoo/morphyn?style=social)
![License](https://img.shields.io/badge/license-Apache%202.0-blue)
![Unity](https://img.shields.io/badge/Unity-2020.3+-black)
![Status](https://img.shields.io/badge/status-beta-orange)

</div>

---

## C# vs Morphyn

<table>
<tr>
<th width="50%">❌ Without Morphyn</th>
<th width="50%">✅ With Morphyn</th>
</tr>
<tr>
<td>

```csharp
public class Player : MonoBehaviour {
  public int exp;
  public int level = 1;

  public void AddExp(int amount) {
    exp += amount;
    if (exp >= 100) {
      level++;
      exp = 0;
      maxHp += 20;
      hp = maxHp;
      Debug.Log("LEVEL UP!");
    }
  }
}
```
Change logic or add new rules → exit Play Mode → recompile → test → repeat

</td>
<td>

```morphyn
entity Player {
  has exp: 0
  has level: 1

  on add_exp(amount) {
    exp + amount -> exp
    check exp >= 100: {
      level + 1 -> level
      0 -> exp
    }
  }
}
```
Change anything → save → **game updates instantly**

</td>
</tr>
</table>

Game state is preserved across reloads. Position, inventory, quest flags — all intact.

---

## Quick Start

**1.** Download and import [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2.** Create a `.morphyn` file:
```morphyn
entity Player {
  has hp: 100
  has damage: 25
}
```

**3.** Use in C#:
```csharp
using Morphyn.Unity;

double hp = MorphynController.Instance.GetField("Player", "hp");
MorphynController.Instance.SendEventToEntity("Player", "damage", 50);
```

**4.** Add `MorphynController` to your scene, drag in the `.morphyn` files, check **Enable Hot Reload**, press Play.

Full docs at [jvnkoo.github.io/morphyn](https://jvnkoo.github.io/morphyn).

---

## What It's Good For

| Use case | Example |
|---|---|
| Game balance | HP, damage, speed, spawn rates |
| Economy systems | Prices, stock, discounts |
| Quest logic | Conditions, triggers, rewards |
| AI behavior | Aggro range, patrol timing |
| Difficulty scaling | Dynamic stat multipliers |

**Rule of thumb:** if you'd normally reach for a ScriptableObject, reach for Morphyn instead. Not designed for 3D math, performance-critical loops, or UI rendering.

---

## Built-in Validation

```morphyn
on heal(amount) {
  check amount > 0: hp + amount -> hp
  check hp > max_hp: max_hp -> hp
}

on buy_item(cost) {
  check gold >= cost: {
    gold - cost -> gold
    emit inventory.add("sword")
  }
  check gold < cost: emit show_error("Not enough gold")
}
```

No more defensive `if` chains in C#.

---

## Morphyn vs Alternatives

| | ScriptableObjects | JSON / YAML | **Morphyn** |
|---|---|---|---|
| Hot reload | ❌ | ❌ | ✅ |
| Logic & events | ❌ | ❌ | ✅ |
| Built-in validation | ❌ | ❌ | ✅ |
| State preserved on reload | ❌ | ❌ | ✅ |

---

## VS Code Extension

Syntax highlighting, bracket matching, comment support for `.morphyn` files.

[📥 Download `.vsix` from Releases](https://github.com/jvnkoo/morphyn/releases/latest) → Extensions → `...` → Install from VSIX

---

## Standalone Runtime

Works with any .NET project, no Unity required.

```bash
# Linux / macOS
./install.sh && morphyn game.morphyn

# Windows
.\install.ps1; morphyn game.morphyn
```

---

## FAQ

**Is this production-ready?**  
Beta. Core features are stable, API may change before v1.0.

**Does hot reload work in builds?**  
No — editor only. Builds run Morphyn normally without file watching.

**Performance impact?**  
Negligible. Morphyn handles config and logic, not hot paths.

**Learning curve?**  
If you can read pseudocode, you can write Morphyn. Most people are productive in under 10 minutes.

**What's the license?**  
Apache 2.0. Free for commercial use, attribution required.

---

## Roadmap

- [x] Core language runtime
- [x] Unity integration  
- [x] Hot reload system
- [x] VS Code extension
- [ ] Async event handling
- [ ] More documentation examples
- [ ] Performance optimizations
- [ ] Transcending the need for C# altogether (eventually, hopefully)

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
