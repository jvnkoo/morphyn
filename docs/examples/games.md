# Game Examples

## Complete Game Example
```morphyn
import "player.morphyn";
import "enemy.morphyn";

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