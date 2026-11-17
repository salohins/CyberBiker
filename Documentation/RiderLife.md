# Rider Life

The existing `Player_Last` prefab now includes **Rider Life**. Restart Play mode after Unity imports the script and prefab changes.

The rider briefly notices nearby traffic and enemies, then returns to the authored forward riding pose. A close traffic pass adds a small shoulder turn and lean away from the passing car. Slow breathing and a little idle sway keep the body from feeling frozen. Passing reactions use the existing closest-pass detection and still work with audio muted.

The original riding helmet pitch is preserved. When idle, the authored helmet pitch is retained. Car glances use the roof/cabin as a 3D look point and share the turn between the neck and helmet. Their pitch change is bounded (25 degrees by default) and blends back after the car passes. Enemy-looking behavior retains its existing settings. The separate forward-level correction on **Bike Auto Aim** now applies only while holding the pistol without a target.

A visible enemy firing at the player has the highest head-attention priority, even when the pistol targets a different enemy. He interrupts an existing car glance, remains the focus between shots, and blocks new passing-car distractions while visible. If several attackers are visible, a recent shooter is preferred. The selected aim target is a fallback when there is no visible firing enemy. Ambient torso motion and traffic kicks yield to the weapon pose. Sword draw/strike/sheathing/recovery, collision recovery, and projectile dodge poses also have priority. Rider Life changes bone rotations only; it does not steer the motorcycle, move the player, or change cameras, physics or damage.

Tune **Player_Last → Rider Life** in the Inspector:

- **Brief glances while riding:** traffic/enemy notice distances, head turn limits, glance duration/cooldown and smoothing.
- **Car anticipation and neck movement:** anticipate a pass from closing speed (1.1 seconds by default), car look/return smoothing, neck share (40% by default), and car pitch range. Urgent approaching cars can bypass the long idle-glance cooldown after a short forward-looking break.
- **Body and wind response:** body follow, pass lean/recovery, breathing and idle sway. Set any amplitude to zero to remove that part.
- **Head follows the selected enemy while aiming:** fallback toggle and vertical look limit. Visible firing enemies take priority independently of this fallback.
- **Combat Head Response / Shooting Enemy Range:** faster threat tracking and maximum visible-shooter notice distance.
- **Reactions Enabled:** disables the whole additive layer and restores the underlying pose.

Car anticipation uses the existing traffic registry and velocity; the test fixture is noticed at 50 m instead of waiting for the old 20 m threshold. Active traffic/enemy registries are scanned at a limited rate, with no scene searches or allocations in the frame loop. Native Unity checks cover preservation of the original riding head angle, traffic/enemy attention, actual pass detection while muted, return/recovery, weapon alignment and exclusive sword animation ownership. Evidence is in `Documentation/RiderLifeValidation`.
