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
[CreateAssetMenu]
public class EnemyData : ScriptableObject {
    public float hp = 100;
    public float damage = 15;
    public float aggroRange = 10f;
    public float spawnInterval = 2f;
}

public class Enemy : MonoBehaviour {
    public EnemyData data;
    float hp;

    void Start() => hp = data.hp;

    public void TakeDamage(float amount) {
        hp -= amount;
        if (hp <= 0) Destroy(gameObject);
    }
}
```

Change `spawnInterval` → exit Play Mode → wait → press Play → test → repeat

</td>
<td>

```morphyn
entity Enemy {
  has hp: 100
  has damage: 15
  has aggro_range: 10
  has spawn_interval: 2000

  on take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit self.destroy
  }
}
```

Change anything → save → **updates while the game runs**

</td>
</tr>
</table>

Game state is preserved across reloads. Position, inventory, quest flags — all intact.

---
## Quick Start

**1.** Download and import [`Morphyn.unitypackage`](https://github.com/jvnkoo/morphyn/releases/latest)

**2.** Create a `.morph` file:
```morphyn
entity Enemy {
  has hp: 100
  has damage: 15

  on take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit self.destroy
  }
}
```

**3.** Use in C#:
```csharp
using Morphyn.Unity;

double hp = Convert.ToDouble(MorphynController.Instance.GetField("Enemy", "hp"));
MorphynController.Instance.Emit("Enemy", "take_damage", 25);
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

## FAQ

**Is this production-ready?**  
Beta. Core features are stable, API may change before v1.0.

**Does hot reload work in builds?**  
No — editor only. Builds run Morphyn normally without file watching.

**Performance impact?**  
Negligible. Morphyn handles config and logic, not hot paths.

**Learning curve?**  
If you can read pseudocode, you can write Morphyn. Most people are productive in under 10 minutes.

**Can Morphyn code crash the runtime?**  
No. The event queue architecture makes it structurally impossible:
- Infinite loops spin in the queue without growing the call stack
- Event deduplication blocks flood attacks
- All exceptions are caught per-event — one bad event never kills the runtime
- Sync recursion is physically blocked by a flag

The only way to kill the process is OOM from unbounded pool growth — and that's the OS, not Morphyn.

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
