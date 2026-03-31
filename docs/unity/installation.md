# Unity Installation

## Installation Steps

1. Download `Morphyn.unitypackage` from [Releases](https://github.com/jvnkoo/morphyn/releases/latest)
2. Import: `Assets > Import Package > Custom Package`
3. **Optional global controller:** `GameObject > Create Empty > Add Component > Morphyn Controller`
4. Press Play

Done.

---

## Setup Options

### Option A: MorphynEntity per GameObject (Recommended)

Attach a `MorphynEntity` component directly to each GameObject that needs its own script.

1. Select a GameObject in the scene or a prefab
2. `Add Component > Morphyn Entity`
3. Drag a `.morph` file into the **Script** slot
4. Optionally set a **Custom Entity Name** (defaults to the file name)
5. Press Play — the entity is registered automatically

No global `MorphynController` is required for this workflow, but one must exist somewhere in the scene for the shared runtime to run.

### Option B: MorphynController (Global)

Load all `.morph` files from a single scene-level component.

1. `GameObject > Create Empty > Add Component > Morphyn Controller`
2. Drag `.morph` files into the **Morphyn Scripts** array
3. Set per-file **Save Mode** if needed
4. Check **Enable Hot Reload** for live editing
5. Press Play

---

## Quick Start Example

### Step 1: Create a Config File

Create `player.morph` in your Assets folder:

```morphyn
entity Player {
  has hp: 100
  has damage: 25
  has level: 1

  event level_up {
    level + 1 -> level
    hp + 20 -> hp
    emit unity("Log", "Level up! New level:", level)
  }
}
```

### Step 2a: Entity Component Approach

Add `MorphynEntity` to your Player GameObject and drag `player.morph` into the Script slot.

```cs
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    MorphynEntity _entity;

    void Start()
    {
        _entity = GetComponent<MorphynEntity>();
        float hp = _entity.Get<float>("hp", 100f);
        Debug.Log($"Player HP: {hp}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            _entity.Emit("level_up");
    }
}
```

### Step 2b: Global Controller Approach

```cs
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    MorphynController _morphyn;

    void Start()
    {
        _morphyn = MorphynController.Instance;
        float hp = _morphyn.GetFloat("Player", "hp");
        Debug.Log($"Player HP: {hp}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            _morphyn.Emit("Player", "level_up");
    }
}
```

### Step 3: Test Hot Reload

1. Enter Play mode
2. Open `player.morph`
3. Change `has hp: 100` to `has hp: 999`
4. Save
5. **HP updates instantly in the running game!**

---

## Inspector

When a `MorphynEntity` component is attached to a GameObject, the Inspector shows:

- **Fields** — all `has` fields with their current values, editable in both Edit and Play mode. Editing a field in Edit mode patches the `.morph` file directly.
- **Events** — all defined events with an **Emit** button to fire them manually from the Inspector during Play mode.
- A status message showing how many fields and events were parsed.

---

## Next Steps

- [Unity API Reference](api.md)
- [Learn in Y minutes](learn-unity.md)