# Morphyn - Hot Reload for Game Logic

<div align="center">
<img src="https://github.com/user-attachments/assets/7c061775-2683-4dd6-acf5-01a5835acf60" width="256" height="256" alt="Morphyn Logo" />

## Stop waiting 30 seconds every time you change a number.

**Edit game balance in real-time. See changes instantly.**

[📥 Download](https://github.com/jvnkoo/morphyn/releases/latest) • [📖 Docs](https://jvnkoo.github.io/morphyn) • [💡 Examples](https://jvnkoo.github.io/morphyn/examples/basic/)

![GitHub stars](https://img.shields.io/github/stars/jvnkoo/morphyn?style=social)
![License](https://img.shields.io/badge/license-Apache%202.0-blue)
![Unity](https://img.shields.io/badge/Unity-2020.3+-black)
![Status](https://img.shields.io/badge/status-beta-orange)

</div>

---

## The Problem

You're balancing an enemy. You change HP from `100` to `150`.  
**Stop Play mode** → **Wait for Unity to recompile** → **Press Play** → **Navigate back to the enemy**.

Repeat this 50 times a day. You've just lost an hour of your life to a progress bar.

## The Solution
```morphyn
entity Enemy {
  has hp: 100      // ← Change this to 150
  has damage: 25   // ← Change this to 999
}
```

**Save the file. Game updates instantly. While running.**

---

## What Is Morphyn?

**Think of it as JSON that can think.**

| Feature | Regular JSON / SO | **Morphyn** |
| :--- | :--- | :--- |
| **Content** | Static Data only | Data + Logic + Events |
| **Validation** | Manual C# checks | Self-validating (`check` syntax) |
| **Iteration** | Recompile on change | **Instant Hot Reload** |
| **State** | Lost on recompile | **Preserved during reload** |

**Example: Level-up system**

<table>
<tr>
<td width="50%">

**❌ The C# Way** (50 lines)
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
      // ... more boilerplate
    }
  }
}
```
*Change logic = stop game & recompile*

</td>
<td width="50%">

**✅ The Morphyn Way** (10 lines)
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
*Change logic = instant hot reload*

</td>
</tr>
</table>

---

## Quick Start

### Unity Integration

**1. Download** [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2. Import** into Unity project

**3. Create config** (`player.morphyn`)
```morphyn
entity Player {
  has hp: 100
  has damage: 25
}
```

**4. Use in C#**
```csharp
using Morphyn.Unity;

// Read values
double hp = MorphynController.Instance.GetField("Player", "hp");

// Send events
MorphynController.Instance.SendEventToEntity("Player", "damage", 50);
```

**5. Enable hot reload**
- Add `MorphynController` component to scene
- Drag `.morphyn` files into the inspector
- Check `Enable Hot Reload`
- Press Play

**6. Edit while playing**
- Change `has hp: 100` to `has hp: 999`
- Save file
- **Value updates instantly** ✨

### Standalone Runtime

**Download from [Releases](https://github.com/jvnkoo/morphyn/releases/latest):**
- Runtime: `morphyn-windows-x64.exe` (Windows) / `morphyn-linux-x64` (Linux/macOS)
- Install script: `install.ps1` / `install.sh`

**Setup:**
1. Download both files for your platform
2. Run install script to add `morphyn` to PATH:
   - Windows: `.\install.ps1`
   - Linux/macOS: `./install.sh`

> [!NOTE]
> The install script only needs to be run once.

**3. Create a file** (`game.morphyn`)
```morphyn
entity Game {
  has score: 0
  on init {
    emit log("Game started!")
  }
}
```

**4. Run it**
```bash
morphyn game.morphyn
```

---

## Why Use Morphyn?

### ⚡ Hot Reload Logic, Not Just Values
Change entire event handlers without restarting:
```morphyn
on damage(amount) {
  hp - amount -> hp
  check hp <= 0: emit die  # ← Change condition while game runs
}
```

### 🛡️ Built-in Validation
```morphyn
on heal(amount) {
  check amount > 0: hp + amount -> hp  # Auto-validates
  check hp > max_hp: max_hp -> hp      # Auto-clamps
}
```
No more manual `if` checks in C#.

### 🎯 Made for Game Logic
```morphyn
entity Shop {
  has gold: 100
  
  on buy_item(cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit inventory.add("sword")
    }
    check gold < cost: emit show_error("Not enough gold")
  }
}
```

---

## Real-World Use Cases

### ✅ Perfect For
- **Game balance** (HP, damage, spawn rates)
- **Shop systems** (prices, discounts, stock)
- **Quest logic** (conditions, rewards)
- **AI behavior** (aggro ranges, patrol patterns)
- **Difficulty settings** (dynamic scaling)

### ⚠️ Not Ideal For
- Complex 3D math (use C#)
- Performance-critical code (use C#)
- UI rendering (use Unity's UI system)

**Rule of thumb:** If you'd normally put it in a ScriptableObject, use Morphyn instead.

---

## Documentation

📖 **[Full Documentation](https://jvnkoo.github.io/morphyn)**

Quick links:
- [Language Syntax](https://jvnkoo.github.io/morphyn/language/syntax)
- [Unity API Reference](https://jvnkoo.github.io/morphyn/unity/api)
- [Code Examples](https://jvnkoo.github.io/morphyn/examples/basic)
- [Troubleshooting](https://jvnkoo.github.io/morphyn/unity/api#troubleshooting)

---

## Tools & Extensions

### VS Code Extension
**[📥 Download from Releases](https://github.com/jvnkoo/morphyn/releases/latest)**

Features:
- Syntax highlighting
- Bracket matching
- Comment support

Manual install:
1. Download `.vsix` file from releases
2. Open VS Code
3. Extensions → `...` → Install from VSIX

---

## FAQ

**Q: Is this production-ready?**  
A: Currently in **beta**. Core features are stable, but API may change before v1.0.

**Q: What if you abandon the project?**  
A: Apache 2.0 license. Code is yours forever. Simple architecture makes forking easy.

**Q: Performance impact?**  
A: Negligible. Morphyn is used for config/logic, not performance-critical loops.

**Q: Can I use it without Unity?**  
A: Yes! Standalone runtime works with any .NET project.

**Q: How hard is it to learn?**  
A: If you can read pseudocode, you can read Morphyn. ~10 minute learning curve. (Python is harder)

**Q: Does hot reload work in builds?**  
A: No, hot reload is **editor-only**. Builds run normally without file watching.

---

## Community & Support

- 🐛 [Report Issues](https://github.com/jvnkoo/morphyn/issues)
- 💡 [Feature Requests](https://github.com/jvnkoo/morphyn/issues) (use "enhancement" label)

**Want to contribute?** PRs are welcome! Check the [issues page](https://github.com/jvnkoo/morphyn/issues) for good first issues.

---

## Roadmap

- [x] Core language runtime
- [x] Unity integration
- [x] Hot reload system
- [x] VS Code extension
- [ ] Asynchronous event handling
- [ ] More documentation examples
- [ ] Performance optimizations
- [ ] Community feedback integration
- [ ] Transcending the need for C# altogether (eventually, hopefully)

---

## License

Apache 2.0 - See [LICENSE](LICENSE) and [NOTICE](NOTICE)

Free for commercial use. Attribution required per Apache 2.0 terms.

---

<div align="center">

**Made by gamedevs, for gamedevs.**

[⭐ Star this repo](https://github.com/jvnkoo/morphyn) if Morphyn saves you time!

<img src="https://media1.tenor.com/m/ugRQCY7AKEsAAAAd/texh-texhnolyze.gif" width="1000" height="300" alt="gif">

</div>

> P.S. Morphyn is not a drug, but the development speed is addictive.

