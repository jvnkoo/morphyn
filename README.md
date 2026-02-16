# The Morphyn Programming Language

<div align="center">
  <img src="https://github.com/user-attachments/assets/7c061775-2683-4dd6-acf5-01a5835acf60" width="256" height="256" alt="Morphyn Logo" />
</div>

Morphyn is an event-driven language for game logic and smart configurations. It replaces JSON with executable logic, enabling reactive data flows without control flow complexity.

**Key Features:**
- Event-driven architecture (no loops, no branches)
- Hot reload support for instant iteration
- Built-in validation and safety checks
- Simple, readable syntax
- Native Unity integration

```morphyn
entity Player {
  has hp: 100
  has level: 1
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit die
  }
  
  on levelUp {
    level + 1 -> level
    emit log("Level up!")
  }
}
```

## Documentation

Full language documentation and examples available at:  
**https://jvnkoo.github.io/morphyn**

## Download and Install

### Binary Releases

Official releases available at:  
**https://github.com/jvnkoo/morphyn/releases**

### Unity Package

Download `Morphyn.unitypackage` from releases for Unity integration.

### Build from Source

```bash
git clone https://github.com/jvnkoo/morphyn.git
cd morphyn
dotnet build
```

Run a Morphyn program:
```bash
morphyn program.morphyn
```

## Quick Start

Create a file `hello.morphyn`:
```morphyn
entity Game {
  has message: "Hello, Morphyn!"
  
  on init {
    emit log(message)
  }
}
```

Run it:
```bash
morphyn hello.morphyn
```

> [!NOTE]
> To access the call via the "morphyn" command, run install.sh (for Linux) or install.ps1 (for Windows) from releases

## Unity Integration

1. Import `Morphyn.unitypackage`
2. Add `MorphynController` component to scene
3. Drag `.morphyn` files into the controller
4. Use Morphyn as smart configs with built-in logic

> [!TIP]
> Development for Unity-specific features is active in the [`unity-bridge`](https://github.com/jvnkoo/morphyn/tree/unity-bridge) branch.

See [Unity documentation](https://jvnkoo.github.io/morphyn/unity/overview/) for details.

## Contributing

Morphyn is open source and welcomes contributions!

Found a bug or have a feature idea? Open an issue on [GitHub](https://github.com/jvnkoo/morphyn/issues).

**Main Branching:**
- **Core Runtime:** Development happens on the `main` branch.
- **Unity Integration:** Use the `unity-bridge` branch.
- **VS Code Extension:** Use the `vscode-extension` branch.

## Philosophy

Traditional programming uses control flow (loops, if-else) to manipulate data.  
Morphyn uses **data flow** (reactions, events) to describe behavior.

Think of entities as worlds with their own physics:
- `has` - the laws of that world
- `on` - reactions to events  
- `emit` - the physics of interaction
- `check` - guards for invariants
- `->` - the flow of energy/data

## License

Morphyn source files are distributed under the Apache License 2.0. See [LICENSE](LICENSE) for details.

## Links

- **Documentation:** https://jvnkoo.github.io/morphyn/
- **GitHub:** https://github.com/jvnkoo/morphyn
- **Releases:** https://github.com/jvnkoo/morphyn/releases

---

<img src="https://media1.tenor.com/m/ugRQCY7AKEsAAAAd/texh-texhnolyze.gif" width="1000" height="300" alt="gif">
