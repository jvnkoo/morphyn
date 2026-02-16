# Unity Installation

## Installation Steps

1. Download `Morphyn.unitypackage` from [GitHub Releases](https://github.com/yourusername/morphyn/releases)
2. Import: `Assets > Import Package > Custom Package`
3. Add to scene: `GameObject > Create Empty > Add Component > Morphyn Controller`
4. Drag .morphyn files into `Morphyn Scripts` array
5. Enable `Run On Start`

Done.

## Quick Start Example

### Step 1: Create Config File

Right-click in Project: `Create > Morphyn File`
```morphyn
entity GameSettings {
  has playerSpeed: 5.0
  has enemyDamage: 10
  has maxEnemies: 50
  has difficulty: "normal"
  
  on set_difficulty(mode) {
    mode -> difficulty
    
    check difficulty == "hard": {
      15 -> enemyDamage
      100 -> maxEnemies
    }
    
    check difficulty == "easy": {
      5 -> enemyDamage
      20 -> maxEnemies
    }
  }
}
```

### Step 2: Read Values from Unity
```cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Read config values
        double speed = System.Convert.ToDouble(
            MorphynController.Instance.GetField("GameSettings", "playerSpeed")
        );
        
        double damage = System.Convert.ToDouble(
            MorphynController.Instance.GetField("GameSettings", "enemyDamage")
        );
        
        Debug.Log($"Speed: {speed}, Damage: {damage}");
    }
    
    public void SetHardMode()
    {
        // Trigger logic inside config
        MorphynController.Instance.SendEventToEntity("GameSettings", "set_difficulty", "hard");
        
        // Values auto-updated!
        double newDamage = System.Convert.ToDouble(
            MorphynController.Instance.GetField("GameSettings", "enemyDamage")
        );
        Debug.Log($"Hard mode damage: {newDamage}"); // 15
    }
}
```