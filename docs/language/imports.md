# Import System

## Import Syntax

Import other Morphyn files:
```morphyn
import "enemies.morphyn"
import "core/weapons.morphyn"
import "../shared/items.morphyn"

entity Player {
  # Can use entities from imported files
}
```

## Import Rules

- Imports are resolved relative to the importing file
- Circular imports are automatically prevented
- Import statements must end with semicolon
- Missing import files generate warnings but don't stop execution

## Examples

### Basic Import

**main.morphyn:**
```morphyn
import "player.morphyn"
import "enemy.morphyn"

entity Game {
  on init {
    emit log("Game started")
  }
}
```

### Subdirectory Imports
```morphyn
import "entities/player.morphyn"
import "entities/enemy.morphyn"
import "systems/combat.morphyn"
```

### Relative Imports
```morphyn
import "../shared/utils.morphyn"
import "../../core/base.morphyn"
```