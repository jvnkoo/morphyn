# Expression System

## Expression Types

### Literals

#### Numbers
```morphyn
100           # Integer
3.14          # Floating point
-42           # Negative
```

#### Strings
```morphyn
"Hello"
"Player Name"
```

#### Booleans
```morphyn
true
false
```

#### Null
```morphyn
null
```

### Variables

#### Entity Fields
```morphyn
hp
name
alive
```

#### Event Parameters
```morphyn
on damage(amount) {
  hp - amount -> hp  # 'amount' is a parameter
}
```

### Arithmetic Operators

#### Basic Math
```morphyn
hp + 10       # Addition
hp - 5        # Subtraction
damage * 2    # Multiplication
armor / 3     # Division
level % 5     # Modulo
```

#### Complex Expressions
```morphyn
(hp + shield) * 0.5 -> total_defense
damage * (1 - armor / 100) -> final_damage
```

### Comparison Operators
```morphyn
hp > 0           # Greater than
level >= 10      # Greater or equal
hp < max_hp      # Less than
mana <= 0        # Less or equal
state == "idle"  # Equal
hp != 0          # Not equal
```

### Logic Operators

#### AND
```morphyn
check hp > 0 and mana > 10: emit cast_spell
```

#### OR
```morphyn
check state == "idle" or state == "walk": emit can_interact
```

#### NOT
```morphyn
check not dead: emit move
check not (hp < 10 and mana < 5): emit safe_to_fight
```

### Pool Access

#### Get Pool Size
```morphyn
enemies.count -> num_enemies
```

#### Access by Index (1-based)
```morphyn
enemies.at[1] -> first_enemy
items.at[i] -> current_item
```

#### Access Entity Fields
```morphyn
player.hp -> player_health
enemy.damage -> incoming_damage
```