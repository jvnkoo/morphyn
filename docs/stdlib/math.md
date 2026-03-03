# Math Library

The `math` entity is a built-in standard library. Import it once and call any function with `emit math.functionName(args) -> result`.

```morphyn
import "math"

entity game {
  has hp: 75
  has max_hp: 100

  event init {
    emit math.clamp(hp, 0, max_hp) -> hp
    emit math.sqrt(16) -> root
    emit log(root)   # 4.0
  }
}
```

---

## Usage Pattern

All math functions are called via sync emit on the `math` entity. Results are written to a local variable or field:

```morphyn
emit math.abs(-5) -> value        # value = 5.0
emit math.lerp(0, 100, 0.5) -> x  # x = 50.0
emit math.sin(math.PI) -> s       # s ≈ 0.0
```

Functions that return multiple values write to `math.out_x`, `math.out_y`, `math.out_z`:

```morphyn
emit math.normalize_2d(3, 4)
math.out_x -> nx   # 0.6
math.out_y -> ny   # 0.8
```

---

## Constants

```morphyn
math.PI       # 3.1415926535
math.TAU      # 6.2831853071  (2 * PI)
math.E        # 2.7182818284
math.EPSILON  # 0.000001
math.INFINITY # 1000000000.0
```

---

## Basic Arithmetic

### abs
Returns the absolute value of a number.
```morphyn
emit math.abs(-5) -> result    # 5.0
emit math.abs(3.14) -> result  # 3.14
```

### min
Returns the smaller of two values.
```morphyn
emit math.min(10, 5) -> result  # 5.0
```

### max
Returns the larger of two values.
```morphyn
emit math.max(10, 5) -> result  # 10.0
```

### clamp
Clamps a value between a minimum and maximum.
```morphyn
emit math.clamp(150, 0, 100) -> result  # 100.0
emit math.clamp(-5, 0, 100) -> result   # 0.0
emit math.clamp(50, 0, 100) -> result   # 50.0
```

### sign
Returns `1.0` for positive, `-1.0` for negative, `0.0` for zero.
```morphyn
emit math.sign(5) -> result   # 1.0
emit math.sign(-3) -> result  # -1.0
emit math.sign(0) -> result   # 0.0
```

---

## Rounding

### floor
Rounds down to the nearest integer.
```morphyn
emit math.floor(3.7) -> result   # 3.0
emit math.floor(-2.3) -> result  # -3.0
```

### ceil
Rounds up to the nearest integer.
```morphyn
emit math.ceil(3.2) -> result   # 4.0
emit math.ceil(-2.7) -> result  # -2.0
```

### round
Rounds to the nearest integer (0.5 rounds up).
```morphyn
emit math.round(3.5) -> result  # 4.0
emit math.round(3.4) -> result  # 3.0
```

### snap
Snaps a value to the nearest grid step.
```morphyn
emit math.snap(13, 10) -> result  # 10.0
emit math.snap(17, 10) -> result  # 20.0
```

### to_fixed
Rounds to a specific number of decimal places.
```morphyn
emit math.to_fixed(3.14159, 2) -> result  # 3.14
```

---

## Interpolation

### lerp
Linear interpolation between `a` and `b` by factor `t`. `t=0` returns `a`, `t=1` returns `b`.
```morphyn
emit math.lerp(0, 100, 0.5) -> result   # 50.0
emit math.lerp(0, 100, 0.25) -> result  # 25.0
```

### lerp_clamped
Same as `lerp` but `t` is clamped to `[0, 1]` — no extrapolation outside the range.
```morphyn
emit math.lerp_clamped(0, 100, 1.5) -> result  # 100.0
```

### lerp_2d
Interpolates two 2D vectors. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.lerp_2d(0, 0, 10, 10, 0.5)
math.out_x -> px  # 5.0
math.out_y -> py  # 5.0
```

### lerp_3d
Interpolates two 3D vectors. Result in `math.out_x`, `math.out_y`, `math.out_z`.
```morphyn
emit math.lerp_3d(0, 0, 0, 10, 10, 10, 0.5)
math.out_x -> px  # 5.0
math.out_y -> py  # 5.0
math.out_z -> pz  # 5.0
```

### inverse_lerp
Finds `t` such that `lerp(a, b, t) == value`.
```morphyn
emit math.inverse_lerp(0, 100, 50) -> result  # 0.5
```

### smooth_step
Smooth hermite interpolation — eases in and out.
```morphyn
emit math.smooth_step(0, 1, 0.5) -> result  # 0.5 (on smooth curve)
```

### slerp_angle
Spherical linear interpolation for angles — constant angular velocity.
```morphyn
emit math.slerp_angle(0, 90, 0.5) -> result  # 45.0
```

### catmull_rom
Catmull-Rom spline through 4 control points (1D).
```morphyn
emit math.catmull_rom(0, 1, 2, 3, 0.5) -> result
```

### catmull_rom_2d
Catmull-Rom spline for 2D paths. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.catmull_rom_2d(p0x, p0y, p1x, p1y, p2x, p2y, p3x, p3y, t)
```

### spring
Physical spring towards a target with momentum. Pass `out_velocity` back each frame.
```morphyn
emit math.spring(current, target, velocity, stiffness, damping, dt) -> current
math.out_velocity -> velocity
```

---

## Normalization & Mapping

### normalize
Maps a value from `[min, max]` to `[0, 1]`.
```morphyn
emit math.normalize(50, 0, 100) -> result  # 0.5
```

### remap
Maps a value from one range to another.
```morphyn
emit math.remap(50, 0, 100, 0, 200) -> result  # 100.0
```

### move_towards
Moves `current` towards `target` by at most `max_delta`.
```morphyn
emit math.move_towards(10, 20, 3) -> result  # 13.0
```

### move_towards_angle
Moves an angle towards a target by at most `max_delta`, taking the shortest path.
```morphyn
emit math.move_towards_angle(350, 10, 15) -> result  # 5.0
```

---

## Distance & Magnitude

### distance_1d
Absolute distance between two values on a line.
```morphyn
emit math.distance_1d(10, 5) -> result  # 5.0
```

### distance_2d
Euclidean distance between two 2D points.
```morphyn
emit math.distance_2d(0, 0, 3, 4) -> result  # 5.0
```

### distance_3d
Euclidean distance between two 3D points.
```morphyn
emit math.distance_3d(0, 0, 0, 1, 1, 1) -> result  # ≈ 1.732
```

### magnitude_2d
Length of a 2D vector.
```morphyn
emit math.magnitude_2d(3, 4) -> result  # 5.0
```

### magnitude_3d
Length of a 3D vector.
```morphyn
emit math.magnitude_3d(1, 2, 2) -> result  # 3.0
```

---

## Power & Roots

### sqrt
Square root using Newton's method (10 iterations).
```morphyn
emit math.sqrt(25) -> result  # 5.0
emit math.sqrt(2) -> result   # ≈ 1.414
```

### inv_sqrt
Inverse square root: `1 / sqrt(x)`.
```morphyn
emit math.inv_sqrt(4) -> result  # 0.5
```

### fast_inv_sqrt
Fast inverse square root approximation — one Newton iteration. Use for performance-critical normalization.
```morphyn
emit math.fast_inv_sqrt(4) -> result  # ≈ 0.5
```

### pow
Power function. Supports integer and fractional exponents.
```morphyn
emit math.pow(2, 3) -> result    # 8.0
emit math.pow(10, -2) -> result  # 0.01
emit math.pow(2, 0.5) -> result  # ≈ 1.414
```

---

## Trigonometry

All angles are in **radians** unless the function name contains `_angle` or `_deg`.

### sin
```morphyn
emit math.sin(math.PI) -> result      # ≈ 0.0
emit math.sin(math.PI / 2) -> result  # ≈ 1.0
```

### cos
```morphyn
emit math.cos(0) -> result        # ≈ 1.0
emit math.cos(math.PI) -> result  # ≈ -1.0
```

### tan
```morphyn
emit math.tan(0.785) -> result  # ≈ 1.0 (PI/4)
```

### asin, acos, atan
Inverse trig functions. Input clamped to `[-1, 1]` for `asin`/`acos`.
```morphyn
emit math.asin(1) -> result   # ≈ 1.5708 (PI/2)
emit math.acos(0) -> result   # ≈ 1.5708 (PI/2)
emit math.atan(1) -> result   # ≈ 0.785  (PI/4)
```

### atan2
Angle in radians from origin to point `(x, y)`. Essential for "look at" direction.
```morphyn
emit math.atan2(1, 1) -> result  # ≈ 0.785 (PI/4)
```

### deg_to_rad / rad_to_deg
```morphyn
emit math.deg_to_rad(180) -> result      # ≈ 3.14159
emit math.rad_to_deg(math.PI) -> result  # 180.0
```

---

## Logarithm & Exponential

### exp_approx
Approximates `e^x` via Taylor series (10 terms). Accurate for `x` in `[-5, 5]`.
```morphyn
emit math.exp_approx(1) -> result  # ≈ 2.718
```

### ln_approx
Approximates natural logarithm `ln(x)` for `x > 0`.
```morphyn
emit math.ln_approx(math.E) -> result  # ≈ 1.0
```

### log10
Base-10 logarithm.
```morphyn
emit math.log10(1000) -> result  # ≈ 3.0
```

### log_base
Logarithm of any base.
```morphyn
emit math.log_base(8, 2) -> result  # ≈ 3.0
```

---

## Angle Operations

### delta_angle
Shortest rotation between two angles in degrees. Returns value in `[-180, 180]`.
```morphyn
emit math.delta_angle(350, 10) -> result   # 20.0 (not 340)
emit math.delta_angle(10, 350) -> result   # -20.0
```

### repeat
Wraps a value between `0` and `length` (modulo).
```morphyn
emit math.repeat(370, 360) -> result   # 10.0
emit math.repeat(-10, 360) -> result   # 350.0
```

### ping_pong
Value bounces back and forth between `0` and `length`.
```morphyn
emit math.ping_pong(0.5, 1) -> result  # 0.5
emit math.ping_pong(1.5, 1) -> result  # 0.5
```

### clamp_angle
Clamps an angle (degrees) between min and max, respecting 360° wrap.
```morphyn
emit math.clamp_angle(270, -45, 45) -> result  # 45.0
```

---

## Vector Operations

### normalize_2d
Normalizes a 2D vector to magnitude 1. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.normalize_2d(3, 4)
math.out_x -> nx  # 0.6
math.out_y -> ny  # 0.8
```

### normalize_3d
Normalizes a 3D vector. Result in `math.out_x`, `math.out_y`, `math.out_z`.
```morphyn
emit math.normalize_3d(1, 0, 0)
math.out_x -> nx  # 1.0
```

### dot_2d
Dot product of two 2D vectors. Positive = same direction, 0 = perpendicular, negative = opposite.
```morphyn
emit math.dot_2d(1, 0, 0, 1) -> result  # 0.0
emit math.dot_2d(1, 0, 1, 0) -> result  # 1.0
```

### dot_3d
Dot product of two 3D vectors.
```morphyn
emit math.dot_3d(1, 0, 0, 1, 0, 0) -> result  # 1.0
```

### cross_product_2d
2D cross product (determinant). Positive = B is left of A, negative = right.
```morphyn
emit math.cross_product_2d(1, 0, 0, 1) -> result  # 1.0
```

### cross_product_3d
3D cross product. Result in `math.out_x`, `math.out_y`, `math.out_z`.
```morphyn
emit math.cross_product_3d(1, 0, 0, 0, 1, 0)
math.out_x -> cx  # 0.0
math.out_y -> cy  # 0.0
math.out_z -> cz  # 1.0
```

### project_2d
Projects vector A onto vector B. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.project_2d(3, 4, 1, 0)
math.out_x -> px  # 3.0
math.out_y -> py  # 0.0
```

### reflect_2d
Reflects a vector off a surface normal. Result in `math.out_x`, `math.out_y`.
```morphyn
# Bullet bouncing off a horizontal floor (normal = 0, 1)
emit math.reflect_2d(1, -1, 0, 1)
math.out_x -> rx  # 1.0
math.out_y -> ry  # 1.0
```

### rotate_vector_2d
Rotates a direction vector by an angle in radians. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.deg_to_rad(90) -> angle_rad
emit math.rotate_vector_2d(1, 0, angle_rad)
math.out_x -> rx  # ≈ 0.0
math.out_y -> ry  # ≈ 1.0
```

### rotate_point
Rotates a point around a center. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.rotate_point(1, 0, 0, 0, math.PI)
math.out_x -> rx  # ≈ -1.0
math.out_y -> ry  # ≈ 0.0
```

### get_perpendicular_2d
Returns a vector 90° counter-clockwise. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.get_perpendicular_2d(1, 0)
math.out_x -> px  # 0.0
math.out_y -> py  # 1.0
```

### smooth_damp
Dampened spring motion. Read `math.out_velocity` and pass it back next frame.
```morphyn
event tick(dt) {
  emit math.smooth_damp(pos_x, target_x, vel_x, smooth_time, dt) -> pos_x
  math.out_velocity -> vel_x
}
```

---

## Geometry & Collision

### point_in_rect
Returns `1.0` if point `(px, py)` is inside rectangle at `(rx, ry)` with size `(rw, rh)`.
```morphyn
emit math.point_in_rect(5, 5, 0, 0, 10, 10) -> result  # 1.0
emit math.point_in_rect(15, 5, 0, 0, 10, 10) -> result  # 0.0
```

### circles_intersect
Returns `1.0` if two circles overlap.
```morphyn
emit math.circles_intersect(0, 0, 5, 8, 0, 5) -> result  # 1.0
```

### rects_intersect
AABB rectangle overlap check.
```morphyn
emit math.rects_intersect(0, 0, 10, 10, 5, 5, 10, 10) -> result  # 1.0
```

### lines_intersect
Returns `1.0` if two line segments intersect.
```morphyn
emit math.lines_intersect(0, 0, 10, 10, 0, 10, 10, 0) -> result  # 1.0
```

### line_intersects_circle
Returns `1.0` if a line segment hits a circle.
```morphyn
emit math.line_intersects_circle(0, 0, 10, 0, 5, 2, 3) -> result  # 1.0
```

### point_in_triangle
Returns `1.0` if point is inside triangle (barycentric coordinates).
```morphyn
emit math.point_in_triangle(5, 5, 0, 0, 10, 0, 5, 10) -> result  # 1.0
```

### triangle_signed_area
Signed area of a triangle. Positive = counter-clockwise vertices.
```morphyn
emit math.triangle_signed_area(0, 0, 10, 0, 5, 5) -> result  # 25.0
```

### distance_point_to_line
Shortest distance from a point to a line segment.
```morphyn
emit math.distance_point_to_line(5, 5, 0, 0, 10, 0) -> result  # 5.0
```

---

## Raycasting & AI

### ray_intersects_circle
Returns distance to hit point, or `-1.0` on miss.
```morphyn
emit math.ray_intersects_circle(0, 0, 1, 0, 5, 0, 2) -> result  # 3.0
```

### is_in_cone
Returns `1.0` if point is within a vision cone.
```morphyn
# Is target at (5, 0) visible from origin, facing right, 90 deg fov, range 10?
emit math.is_in_cone(5, 0, 90, 10, 0, 0, 1, 0) -> result  # 1.0
```

### look_at_angle
Returns the rotation in degrees to face a target point.
```morphyn
emit math.look_at_angle(0, 0, 1, 0) -> result  # 0.0
emit math.look_at_angle(0, 0, 0, 1) -> result  # 90.0
```

---

## Wave Shapes

### triangle_wave
Linear triangle wave between 0 and 1.
```morphyn
emit math.triangle_wave(0.25) -> result  # 0.5
emit math.triangle_wave(0.5) -> result   # 1.0
```

### square_wave
Returns `1.0` or `-1.0`. Use for blinking, pulses, or digital logic.
```morphyn
emit math.square_wave(0) -> result  # 1.0
```

---

## Easing Functions

All easing functions take `t` in `[0, 1]` and return a remapped value.

| Function | Description |
|---|---|
| `ease_in_quad(t)` | Slow start, accelerating |
| `ease_out_quad(t)` | Fast start, decelerating |
| `ease_in_out_quad(t)` | Slow start and end |
| `ease_in_cubic(t)` | Strong slow start |
| `ease_out_cubic(t)` | Strong deceleration |
| `ease_in_out_cubic(t)` | Very smooth |
| `ease_out_back(t)` | Overshoots then settles |
| `ease_in_back(t)` | Pulls back before moving |
| `ease_in_out_back(t)` | Both ends |
| `ease_out_bounce(t)` | Bouncing landing |
| `ease_in_bounce(t)` | Bouncing start |
| `ease_in_out_bounce(t)` | Bounce both ends |

```morphyn
entity ui {
  has t: 0.0
  event tick(dt) {
    t + dt / 1000 -> t
    emit math.ease_out_back(t) -> eased
    emit unity("SetScale", eased)
  }
}
```

---

## Statistics & Ratio

### average_2 / average_3 / average_4
```morphyn
emit math.average_2(10, 20) -> result          # 15.0
emit math.average_3(10, 20, 30) -> result      # 20.0
emit math.average_4(10, 20, 30, 40) -> result  # 25.0
```

### percentage / percent_of / ratio
```morphyn
emit math.percentage(25, 100) -> result   # 25.0
emit math.percent_of(50, 200) -> result   # 100.0
emit math.ratio(3, 4) -> result           # 0.75
```

### in_range / in_range_exclusive
```morphyn
emit math.in_range(5, 0, 10) -> result             # 1.0
emit math.in_range_exclusive(10, 0, 10) -> result  # 0.0
```

### approximately / approximately_default
```morphyn
emit math.approximately(0.1 + 0.2, 0.3, 0.0001) -> result  # 1.0
emit math.approximately_default(0.1 + 0.2, 0.3) -> result  # 1.0
```

---

## Matrix Operations

### 3x3 Matrices

Results write to `math.out_m00` through `math.out_m22` (row-major).

```morphyn
emit math.matrix_identity()
emit math.matrix_translation(10, 5)
emit math.matrix_rotation(math.PI / 4)
emit math.matrix_scaling(2, 2)
```

### matrix_multiply_point
Transforms a 2D point by a 3x3 matrix. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.matrix_translation(10, 5)
emit math.matrix_multiply_point(0, 0, math.out_m00, math.out_m01, math.out_m02, math.out_m10, math.out_m11, math.out_m12)
math.out_x -> px  # 10.0
math.out_y -> py  # 5.0
```

### 4x4 Projection

```morphyn
# fov_rad, aspect, near, far
emit math.matrix_perspective(1.047, 1.777, 0.1, 1000)
# Result in math.out_m00 through math.out_m33
```

---

## Noise & Procedural

### simple_noise
Pseudo-random value in `[0, 1]` from a seed.
```morphyn
emit math.simple_noise(42) -> result
```

### noise_2d
2D pseudo-random noise.
```morphyn
emit math.noise_2d(x, y) -> result
```

### fractal_noise
Fractal Brownian Motion (3 octaves) for organic-looking randomness.
```morphyn
emit math.fractal_noise(seed, 2.0, 0.5) -> result
```

---

## Random

!!! warning
    Not cryptographically secure. Use for game variance only.

### set_random_seed
Seed the RNG — call with frame delta time for variance each frame.
```morphyn
event tick(dt) {
  emit math.set_random_seed(dt)
}
```

### random
Returns a value in `[0, 1]`.
```morphyn
emit math.random -> result
```

### random_range
Returns a value between `min` and `max`.
```morphyn
emit math.random_range(10, 20) -> result  # e.g. 14.537
```

---

## Smoothing

### low_pass_filter
Smooths jittery input (mouse, joystick). `factor` 0.1 = very smooth, 0.9 = very responsive.
```morphyn
event tick(dt) {
  emit math.low_pass_filter(current_value, target_value, 0.1) -> current_value
}
```

---

## Physics

### gravity_force
Newton's universal gravitation.
```morphyn
emit math.gravity_force(m1, m2, distance, 6.674) -> force
```

### air_resistance
Drag force opposing velocity.
```morphyn
emit math.air_resistance(velocity, 0.1) -> drag
```

### resolve_collision
Elastic collision — returns new velocity after impact. `e` is restitution (0–1).
```morphyn
emit math.resolve_collision(v1, v2, m1, m2, 0.8) -> new_v1
```

### pid_update
PID controller for smooth stabilization.
```morphyn
emit math.pid_update(error, last_error, integral, kP, kI, kD, dt) -> correction
```

---

## AI / Neural Activations

### sigmoid
Squashes any value to `(0, 1)`. Standard for probability outputs.
```morphyn
emit math.sigmoid(0) -> result   # 0.5
emit math.sigmoid(5) -> result   # ≈ 0.993
```

### relu
Returns `x` if positive, else `0`. Core of modern neural networks.
```morphyn
emit math.relu(-1) -> result  # 0.0
emit math.relu(3) -> result   # 3.0
```

### tanh
Squashes any value to `(-1, 1)`.
```morphyn
emit math.tanh(0) -> result   # 0.0
emit math.tanh(1) -> result   # ≈ 0.762
```

---

## Bezier Curves

### get_bezier_quadratic_2d
Quadratic bezier — one control point. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.get_bezier_quadratic_2d(0, 0, 50, 100, 100, 0, t)
math.out_x -> bx
math.out_y -> by
```

### get_bezier_cubic_2d
Cubic bezier — two control points. Result in `math.out_x`, `math.out_y`.
```morphyn
emit math.get_bezier_cubic_2d(0, 0, 25, 100, 75, 100, 100, 0, t)
math.out_x -> bx
math.out_y -> by
```

---

## Full Example

```morphyn
import "math"

entity player {
  has x: 0.0
  has y: 0.0
  has target_x: 100.0
  has target_y: 100.0
  has vel_x: 0.0
  has vel_y: 0.0
  has hp: 100.0
  has max_hp: 100.0

  event tick(dt) {
    # Smooth follow with spring damping
    emit math.smooth_damp(x, target_x, vel_x, 0.2, dt) -> x
    math.out_velocity -> vel_x
    emit math.smooth_damp(y, target_y, vel_y, 0.2, dt) -> y
    math.out_velocity -> vel_y

    # Clamp HP just in case
    emit math.clamp(hp, 0, max_hp) -> hp
  }

  event damage(amount) {
    emit math.max(0, hp - amount) -> hp
    check hp <= 0: emit die
  }

  event look_at(tx, ty) {
    emit math.look_at_angle(x, y, tx, ty) -> angle
    emit unity("SetRotation", angle)
  }
}
```