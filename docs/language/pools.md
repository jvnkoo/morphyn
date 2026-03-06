# Pool System

## Overview

Pools are ordered collections of values or entity instances in Morphyn.

## Declaration
```morphyn
entity World {
  has enemies: pool[1, 2, 3]
  has items: pool["sword", "shield"]
  has positions: pool[0.0, 10.5, 20.3]
  has empty: pool[]
}
```

## Pool Commands

### Adding Elements

#### add — append to end (or spawn entity by name)
```morphyn
emit enemies.add("Enemy")  # clones Enemy entity, fires its init
emit items.add("bow")
```

#### push — add to front
```morphyn
emit items.push("new_item")
```

#### insert — insert at position (1-based index)
```morphyn
emit items.insert(2, "middle_item")
```

### Removing Elements

#### remove — remove by value
```morphyn
emit enemies.remove(target)
```

#### remove_at — remove by index (1-based)
```morphyn
emit enemies.remove_at(3)
```

#### pop — remove last element
```morphyn
emit items.pop
```

#### shift — remove first element
```morphyn
emit items.shift
```

#### clear — remove all elements
```morphyn
emit enemies.clear
```

### Other Operations

#### swap — swap two elements (1-based indices)
```morphyn
emit items.swap(1, 3)
```

#### each — call an event on every element
```morphyn
emit enemies.each("take_damage", 10)
emit items.each("collect", player)
```

#### sort — sort elements
```morphyn
emit pool.sort
```

#### reverse — reverse order
```morphyn
emit pool.reverse
```

#### contains — check if pool contains a value
```morphyn
emit pool.contains(value) -> result
```

#### shuffle — randomize order
```morphyn
emit pool.shuffle
```

## Accessing Pools

### Get pool size
```morphyn
enemies.count -> num_enemies
```

### Access by index (1-based)
```morphyn
enemies.at[1] -> first_enemy
enemies.at[i] -> current_enemy
```

### Set by index
```morphyn
new_value -> pool.at[index]
```

### Sync result into pool slot
```morphyn
emit MathLib.abs(scores.at[1]) -> scores.at[1]
```