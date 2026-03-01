# Learn Morphyn in Y Minutes

```morphyn
# This is a comment
// This too
/* And this */

# ── ENTITIES ──────────────────────────────────────────────────────────────────
# Everything in Morphyn lives inside an entity.
# An entity has fields (state) and events (behavior).

entity Player {
  # ── FIELDS ────────────────────────────────────────────────────────────────
  has hp: 100          # number
  has name: "Hero"     # string
  has alive: true      # boolean
  has nothing: null    # null

  # Pool — ordered collection
  has items: pool[]
  has flags: pool[true, false, true]
  has scores: pool[10, 20, 30]

  # ── BUILT-IN EVENTS ───────────────────────────────────────────────────────
  # init runs once when entity is created
  event init {
    emit log("Player created:", name)
  }

  # tick(dt) runs every frame — dt is milliseconds since last frame
  event tick(dt) {
    # use dt to make frame-independent timers
  }

  # ── CUSTOM EVENTS ─────────────────────────────────────────────────────────
  event jump {
    emit log("jumped!")
  }

  event heal(amount) {
    hp + amount -> hp         # data flow: expression -> target
    check hp > max_hp: {
      max_hp -> hp
    }
  }

  # ── DATA FLOW ─────────────────────────────────────────────────────────────
  event examples {
    100 -> hp                 # set
    hp - 10 -> hp             # subtract
    hp * 2 -> doubled         # store in local var
    (hp + 50) * 0.5 -> avg    # expression

    # assign to pool slot (1-based index)
    99 -> scores.at[1]
  }

  # ── CHECK (GUARD) ─────────────────────────────────────────────────────────
  event check_examples {
    # inline action — only runs if condition is true
    check hp > 0: emit log("alive")

    # block action
    check hp <= 0: {
      false -> alive
      emit self.destroy
    }

    # guard — stops event execution if condition is false
    check alive
    emit log("this only runs if alive was true")

    # logic operators
    check hp > 0 and alive: emit log("healthy")
    check hp <= 0 or not alive: emit log("dead")
  }

  # ── EMIT ──────────────────────────────────────────────────────────────────
  event emit_examples {
    emit jump                       # send to self (implicit)
    emit self.jump                  # send to self (explicit)
    emit Enemy.take_damage(25)      # send to another entity
    emit log("value:", hp)          # built-in: print to console
    emit input("Name: ", "name")    # built-in: read console input into field
    emit unity("PlaySound", "hit")  # built-in: call Unity callback
    emit self.destroy               # built-in: mark for garbage collection
  }

  # ── SYNC EMIT (returns a value) ───────────────────────────────────────────
  # Executes immediately, bypassing the event queue.
  # Returns the last assigned value inside the called event.
  event take_damage(amount) {
    emit MathLib.clamp(hp - amount, 0, max_hp) -> hp
    
    # RECURSION & LOOPS:
    # Morphyn now supports deep recursion for implementing while/for loops.
    # The runtime uses a heap-based stack, so 10,000+ iterations are safe.
    check hp > 0: emit self.take_damage(1) -> hp 
  }

  # sync result can also go into a pool slot
  event sync_to_pool {
    emit MathLib.abs(scores.at[1]) -> scores.at[1]
  }

  # ── ARITHMETIC ────────────────────────────────────────────────────────────
  event math {
    hp + 10 -> hp
    hp - 5 -> hp
    hp * 2 -> hp
    hp / 4 -> hp
    hp % 3 -> hp    # modulo
  }

  # ── STRINGS ───────────────────────────────────────────────────────────────
  event strings {
    "Hello" + " " + "World" -> greeting
    check name == "Hero": emit log("is hero")
    check name != "Villain": emit log("not villain")
    # also: > < >= <= for lexicographic comparison
  }

  # ── BLOCK ─────────────────────────────────────────────────────────────────
  event block_example {
    {
      hp + 10 -> hp
      emit log("healed")
    }
  }
}

# ── POOLS ─────────────────────────────────────────────────────────────────────
entity PoolExamples {
  has items: pool["sword", "shield", "potion"]
  has enemies: pool[]

  event init {
    # read
    items.count -> size            # number of elements
    items.at[1] -> first           # get by index (1-based)

    # write
    "axe" -> items.at[2]           # set by index

    # commands
    emit items.add("bow")          # add to end (or spawn entity by name)
    emit items.push("key")         # add to front
    emit items.insert(2, "ring")   # insert at index
    emit items.remove("sword")     # remove by value
    emit items.remove_at(3)        # remove by index
    emit items.pop                 # remove last
    emit items.shift               # remove first
    emit items.swap(1, 2)          # swap two indices
    emit items.clear               # remove all

    # call event on every element
    emit enemies.each("take_damage", 10)

    # spawn entity instances into pool
    emit enemies.add("Enemy")      # clones Enemy entity, fires its init
  }
}

# ── SYNC LIBRARY PATTERN ──────────────────────────────────────────────────────
entity MathLib {
  event clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result               # last assigned = return value
  }

  event abs(value) {
    check value < 0: value * -1 -> value
    value -> result
  }

  event lerp(a, b, t) {
    a + (b - a) * t -> result
  }

  event normalize(value, min, max) {
    (value - min) / (max - min) -> result
  }
}

# ── SUBSCRIPTIONS ─────────────────────────────────────────────────────────────
# when: subscribe to another entity's event
# unwhen: unsubscribe
#
# The optional (arg) after the handler name is evaluated against the SUBSCRIBER
# at the moment the event fires — not the args from the original event.
#
# when Player.die : onPlayerDied           # no args
# when Player.die : onPlayerDied(42)       # fixed literal
# when Player.die : onPlayerDied(myField)  # field — read from subscriber at fire time

entity Logger {
  has severity: 3

  event init {
    when Player.die : onPlayerDied             # subscribe, no args
    when Enemy.die  : onEnemyDied(severity)    # passes Logger.severity at fire time
  }

  event onPlayerDied {
    emit log("Player died")
    unwhen Player.die : onPlayerDied           # unsubscribe — no args to match
  }

  event onEnemyDied(sev) {
    emit log("Enemy died, severity:", sev)
    unwhen Enemy.die : onEnemyDied(severity)   # unwhen args must match the when args
  }
}

# Rules:
# - cannot subscribe to your own instance's events
# - duplicate subscriptions are ignored
# - destroyed entities are cleaned up automatically
# - when/unwhen can be used in any event, not just init
# - unwhen args must match the args used in the original when

# ── IMPORTS ───────────────────────────────────────────────────────────────────
# import "mathlib.morph"
# import "entities/enemy.morph"
# import "../shared/utils.morph"
# Circular imports are prevented automatically.

# ── ENTITY LIFECYCLE ──────────────────────────────────────────────────────────
entity Enemy {
  has hp: 50
  has alive: true

  event init {
    emit log("Enemy spawned")
  }

  event take_damage(amount) {
    hp - amount -> hp
    check hp <= 0: {
      false -> alive
      emit self.die
    }
  }

  event die {
    emit log("Enemy died")
    emit self.destroy               # marks entity for garbage collection
                                    # removed from pools on next GarbageCollect
  }
}

# ── FULL EXAMPLE ──────────────────────────────────────────────────────────────
entity Game {
  has score: 0
  has enemies: pool[]
  has timer: 0

  event init {
    emit enemies.add("Enemy")
    emit enemies.add("Enemy")
    emit log("Game started with", enemies.count, "enemies")
  }

  event tick(dt) {
    timer + dt -> timer
    check timer >= 2000: {
      emit enemies.add("Enemy")
      0 -> timer
    }
  }

  event player_attack(damage) {
    emit enemies.each("take_damage", damage)
  }

  event enemy_died {
    score + 100 -> score
    emit log("Score:", score)
  }
}
```