# Unity Integration

Morphyn runs alongside your Unity project as a scripting layer for game logic and config. `.morph` files are loaded at runtime, hot-reloaded on save, and accessed from C# through a simple API.

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
MorphynController.Instance.On("Player", "die", args => {
    deathScreen.SetActive(true);
});
```

---

## Next Steps

- [Installation](installation.md)
- [API Reference](api.md)
- [Learn in Y minutes](learn-unity.md)