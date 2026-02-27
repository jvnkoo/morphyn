# Language Syntax Reference
## Overview
Morphyn is a declarative language with minimal syntax. Programs consist
of entity declarations with fields and event handlers.
## Comments
Three comment styles are supported:
```morphyn
# Single-line comment (shell style)
// Single-line comment (C++ style)
/* Multi-line comment */
```
## Entity Declaration
```morphyn
entity EntityName {
  # Fields and events
}
```
## Field Declaration
### Basic Fields
```morphyn
has field_name: value
has hp: 100
has name: "Player"
has alive: true
has exist: null
```
### Pool Fields
```morphyn
has items: pool[1, 2, 3]
has names: pool["Alice", "Bob"]
has flags: pool[true, false, true]
```
## Event Handlers
### Without Parameters
```morphyn
on event_name {
  # actions
}
```
### With Parameters
```morphyn
on event_name(param1, param2) {
  # actions
}
```
## Actions
### Data Flow (Arrow)
```morphyn
expression -> target
hp - 10 -> hp
damage * 2 -> result
0 -> counter
```
### Check (Guard)
```morphyn
# Check with an inline action
check condition: action
check hp > 0: emit alive
check state == "idle": emit can_move
# Check without an inline action (guard)
# If false, the event execution is stopped
check i < 0
```
### Emit (Event Dispatch)
```morphyn
emit event_name
emit event_name(arg1, arg2)
emit target.event_name
emit self.destroy
emit log("message", value)
emit input("prompt: ", "fieldName")
```
### Sync Emit (Immediate Call with Return Value)
Executes an event synchronously and assigns the result to a field.
The result is the last value assigned inside the called event.
```morphyn
emit event_name(args) -> field
emit Entity.event_name(args) -> field
emit Entity.event_name(args) -> pool.at[idx]
```
Sync calls can be chained — direct recursion (an event calling itself) is forbidden.
```morphyn
entity MathLib {
  on clamp(value, min, max) {
    check value < min: min -> value
    check value > max: max -> value
    value -> result
  }
}
entity Player {
  has hp: 150
  has max_hp: 100
  on heal(amount) {
    emit MathLib.clamp(hp + amount, 0, max_hp) -> hp
  }
}
```
### Subscriptions
Subscribe to events of another entity:
```morphyn
when TargetEntity.eventName : handlerEvent
unwhen TargetEntity.eventName : handlerEvent
```
```morphyn
entity Logger {
  on init {
    when Player.death : onPlayerDeath
  }
  on onPlayerDeath {
    emit log("Player died!")
  }
}
```
### Block Actions
```morphyn
{
  action1
  action2
  action3
}
```
## Built-in Functions
| Function | Description | Example |
|----------|-------------|---------|
| `log` | Print to console | `emit log("HP:", hp)` |
| `input` | Read line from console into field | `emit input("Name: ", "name")` |
| `unity` | Call Unity callback | `emit unity("PlaySound", "hit")` |
### input
Reads a line from console and writes the result into a field.
The field name must be passed as a **string literal in quotes**.
```morphyn
emit input("Enter your name: ", "name")
emit input("Enter amount: ", "amount")
```
If the input can be parsed as a number it is stored as a number, otherwise as a string.
## Operators
### Arithmetic
| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition | `hp + 10` |
| `-` | Subtraction | `hp - 5` |
| `*` | Multiplication | `damage * 2` |
| `/` | Division | `armor / 3` |
| `%` | Modulo | `level % 5` |
### Comparison
| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Equal | `hp == 100` |
| `!=` | Not equal | `state != "dead"` |
| `>` | Greater than | `hp > 0` |
| `<` | Less than | `hp < max` |
| `>=` | Greater or equal | `level >= 10` |
| `<=` | Less or equal | `mana <= 0` |
### Logic
| Operator | Description | Example |
|----------|-------------|---------|
| `and` | Logical AND | `hp > 0 and alive` |
| `or` | Logical OR | `idle or walk` |
| `not` | Logical NOT | `not dead` |
### Flow
| Operator | Description | Example |
|----------|-------------|---------|
| `->` | Data flow | `value -> field` |
## Keywords
| Keyword | Purpose |
|---------|---------|
| `entity` | Declare entity |
| `has` | Declare field |
| `on` | Declare event handler |
| `emit` | Send event, call sync event, or call built-in function |
| `check` | Conditional guard |
| `pool` | Collection type |
| `when` | Subscribe to another entity's event |
| `unwhen` | Unsubscribe from another entity's event |
| `true` | Boolean true |
| `false` | Boolean false |
| `null` | Null value |