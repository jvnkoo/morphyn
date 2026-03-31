# Unity Integration

Morphyn runs alongside your Unity project as a scripting layer for game logic and config. `.morph` files are loaded at runtime, hot-reloaded on save, and accessed from C# through a simple API.

There are two ways to structure your project:

- **MorphynController** — one global component that loads all `.morph` files. Good for shared config, singletons, and global systems.
- **MorphynEntity** — one component per GameObject, each with its own `.morph` file. Good for per-object logic, prefabs, and modular design.

---

## Entity-First Workflow (Recommended)

Instead of dumping all scripts into one `MorphynController`, attach a `MorphynEntity` component to each relevant GameObject and point it at its own `.morph` file.

```
PlayerObject    → MorphynEntity → player.morph
EnemyPrefab     → MorphynEntity → enemy.morph
ShopManager     → MorphynEntity → shop.morph
```

Each entity is isolated, self-contained, and automatically registered into the shared runtime context when the scene starts.

---

## Hot Reload

Change values or logic in a `.morph` file while the game is running — no stopping Play mode.

```morphyn
entity Enemy {
  has hp: 100      # change this to 999, save, done
  has damage: 25
}
```

---

## Key Features

**Entity-per-object architecture** — each GameObject manages its own `.morph` script:

```cs
// Attach MorphynEntity to a prefab, no global controller needed
public class EnemyController : MonoBehaviour
{
    MorphynEntity _entity;

    void Start()
    {
        _entity = GetComponent<MorphynEntity>();
        _entity.Watch<float>("hp", (old, now) => hpBar.fillAmount = now / 50f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
            _entity.Emit("take_damage", 25);
    }
}
```

**Hot reload** — edit logic and values without restarting:

```morphyn
event damage(amount) {
  hp - amount -> hp
  check hp <= 0: emit die
}
```

**Event-driven logic** — reactive behaviors without state machines:

```morphyn
entity Shop {
  has gold: 100
  event buy(cost) {
    check gold >= cost: {
      gold - cost -> gold
      emit inventory.add("sword")
    }
    check gold < cost: emit unity("ShowError", "Not enough gold")
  }
}
```

**C# bridge** — subscribe Unity methods directly to Morphyn events:

```cs
MorphynController.Instance.When("Player", "die", args => {
    deathScreen.SetActive(true);
});
```

**Field watchers** — react to field value changes from C# or from `.morph`:

```cs
// C#
MorphynController.Instance.Watch<float>("Player", "hp", (old, now) => {
    hpBar.fillAmount = now / maxHp;
});
```

```morphyn
// .morph
entity UI {
  event init {
    watch Player.hp : onPlayerHpChanged
  }
  event onPlayerHpChanged(old, new) {
    emit log("hp:", old, "->", new)
  }
}
```

**Inspector integration** — field values and events are visible and editable in the Unity Inspector when using `MorphynEntity`. Fields reflect live values during Play mode. Events can be fired from the Inspector with a single click.

---

## Next Steps

- [Installation](installation.md)
- [API Reference](api.md)
- [Learn in Y minutes](learn-unity.md)