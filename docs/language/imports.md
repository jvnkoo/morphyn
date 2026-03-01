# Import System

## Import Syntax

Import other Morphyn files:
```morphyn
import "enemies.morph"
import "core/weapons.morph"
import "../shared/items.morph"

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

**main.morph:**
```morphyn
import "player.morph"
import "enemy.morph"

entity Game {
  event init {
    emit log("Game started")
  }
}
```

### Subdirectory Imports
```morphyn
import "entities/player.morph"
import "entities/enemy.morph"
import "systems/combat.morph"
```

### Relative Imports
```morphyn
import "../shared/utils.morph"
import "../../core/base.morph"
```