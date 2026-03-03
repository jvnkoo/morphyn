**Standard Library** — built-in entities, always available via import:
```morphyn
import "math"

entity game {
  event init {
    emit math.sqrt(144) -> result   # 12.0
    emit math.lerp(0, 100, 0.5) -> mid  # 50.0
  }
}
` ``
[Standard Library →](stdlib/index.md)
```

Папку назвать `stdlib` — коротко, понятно, индустриальный стандарт (Python, Rust, Go все используют это слово). `lib` тоже ок, но `stdlib` сразу говорит "это встроенное, не твоё".