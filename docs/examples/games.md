# Game Examples

## Complete Game Example

This example shows a complete game loop with automatic enemy spawning.
```morphyn
import "player.morphyn"
import "enemy.morphyn"

entity Game {
  has score: 0
  has enemies: pool[]
  has spawn_timer: 0
  has spawn_interval: 2000
  
  on init {
    emit log("Game started!")
    emit spawn_enemy
  }
  
  on tick(dt) {
    spawn_timer + dt -> spawn_timer
    
    check spawn_timer >= spawn_interval: {
      emit spawn_enemy
      0 -> spawn_timer
    }
    
    emit enemies.each(update, dt)
  }
  
  on spawn_enemy {
    emit enemies.add(Enemy)
    emit log("Enemy spawned! Total:", enemies.count)
  }
  
  on enemy_killed {
    score + 100 -> score
    emit log("Score:", score)
  }
}
```

**How it works:**

1. **`init`** runs once when game starts
   - Spawns first enemy
   
2. **`tick(dt)`** runs every frame
   - Updates spawn timer
   - Spawns new enemy every 2000ms (2 seconds)
   - Calls `update` event on all enemies

3. **`enemy_killed`** must be triggered externally when enemy dies
   - In Unity: Call from enemy death script
   - In standalone: Enemy can emit `game.enemy_killed` on death

**Enemy definition (`enemy.morphyn`):**
```morphyn
entity Enemy {
  has hp: 50
  
  on update(dt) {
    # Enemy AI logic here
    emit log("Enemy updating")
  }
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: {
      emit game.enemy_killed  # Notify game
      emit self.destroy
    }
  }
}
```

**Unity integration:**
```csharp
public class EnemyController : MonoBehaviour
{
    void OnDestroy()
    {
        // Notify Morphyn when enemy is destroyed
        MorphynController.Instance.SendEventToEntity("Game", "enemy_killed");
    }
}
```