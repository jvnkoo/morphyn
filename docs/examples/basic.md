# Basic Examples

## Simple Counter
```morphyn
entity Counter {
  has value: 0
  
  on init {
    emit log("Counter initialized")
  }
  
  on tick(dt) {
    value + 1 -> value
    emit log("Counter:", value)
  }
}
```

## Health System
```morphyn
entity Player {
  has hp: 100
  has max_hp: 100
  has alive: true
  
  on damage(amount) {
    hp - amount -> hp
    check hp <= 0: {
      0 -> hp
      false -> alive
      emit die
    }
  }
  
  on heal(amount) {
    check alive: {
      hp + amount -> hp
      check hp > max_hp: max_hp -> hp
    }
  }
  
  on die {
    emit log("Player died")
  }
}
```

## Enemy AI
```morphyn
entity Enemy {
  has hp: 50
  has damage: 10
  has state: "idle"
  
  on see_player {
    "chase" -> state
  }
  
  on tick(dt) {
    check state == "chase": emit attack
  }
  
  on attack {
    emit player.damage(damage)
  }
  
  on take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: emit self.destroy
  }
}
```

## Inventory System
```morphyn
entity Inventory {
  has items: pool["sword", "shield"]
  has capacity: 10
  
  on add_item(item) {
    check items.count < capacity: emit items.add(item)
  }
  
  on remove_item(index) {
    emit items.remove_at(index)
  }
  
  on use_item(index) {
    items.at[index] -> current_item
    emit log("Using:", current_item)
  }
  
  on list_items {
    emit log("Inventory size:", items.count)
    emit items.each(show_item)
  }
  
  on show_item(item) {
    emit log("  -", item)
  }
}
```