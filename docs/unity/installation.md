# Unity Installation

## Installation Steps

1. Download `Morphyn.unitypackage` from [Releases](https://github.com/jvnkoo/morphyn/releases/latest)
2. Import: `Assets > Import Package > Custom Package`
3. Add to scene: `GameObject > Create Empty > Add Component > Morphyn Controller`
4. Drag `.morph` files into `Morphyn Scripts` array
5. Check `Enable Hot Reload`
6. Press Play

Done.

## Quick Start Example

### Step 1: Create Config File

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

### Step 2: Use in C#
```cs
using UnityEngine;
using Morphyn.Unity;

public class PlayerController : MonoBehaviour
{
    void Start()
    {
        // Read values
        double hp = System.Convert.ToDouble(
            MorphynController.Instance.GetField("Player", "hp")
        );

        Debug.Log($"Player HP: {hp}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            // Trigger event
            MorphynController.Instance.Emit("Player", "level_up");
        }
    }
}
```

### Step 3: Test Hot Reload

1. Enter Play mode
2. Open `player.morph`
3. Change `has hp: 100` to `has hp: 999`
4. Save
5. **HP updates instantly in running game!**

## Next Steps

- [Unity API Reference](api.md)
- [Learn in Y minutes](learn-unity.md)