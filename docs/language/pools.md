# Pool System

## Overview

Pools are collections of entities or values in Morphyn. They provide
high-performance storage for game objects.

## Declaration
```morphyn
entity World {
  has enemies: pool[1, 2, 3]
  has items: pool["sword", "shield"]
  has positions: pool[0.0, 10.5, 20.3]
}
```

## Pool Commands

### Adding Elements

#### add - Add entity instance
```morphyn
emit enemies.add(Enemy)  # Creates new Enemy and adds to pool
```

#### push - Add to front
```morphyn
emit items.push("new_item")
```

#### insert - Insert at position (1-based index)
```morphyn
emit items.insert(2, "middle_item")
```

### Removing Elements

#### remove - Remove specific value
```morphyn
emit enemies.remove(target)
```

#### remove_at - Remove at index (1-based)
```morphyn
emit enemies.remove_at(3)
```

#### pop - Remove last element
```morphyn
emit items.pop
```

#### shift - Remove first element
```morphyn
emit items.shift
```

#### clear - Remove all elements
```morphyn
emit enemies.clear
```

### Other Operations

#### swap - Swap two elements (1-based indices)
```morphyn
emit items.swap(1, 3)
```

#### each - Call event on each element
```morphyn
emit enemies.each(update, dt)
emit items.each(collect, player)
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