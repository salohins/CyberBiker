# Incoming projectiles and timed evasion

Bullet appearance has been restored to the original projectile prefab: its original mesh, materials, size and tracer. The replacement white core and red tail have been removed. A separate pale air wake now curls behind hostile shots. Damage colliders, speed and committed flight directions are unchanged.

By default, enemy trajectory lines are hidden before firing and green while the bullet flies. The color stays green whether the player is inside or outside the path. The line uses the same moving BulletLocal frame as the bullet and disappears at its committed endpoint or when the projectile is stopped. Permanent gun lasers are hidden. Disable **Enemy Trajectory Guides** to restore the previous mode described next.

In the previous mode enemies keep white lasers visible while aiming. Before each shot, the firing gun's beam turns red for the full 0.6-second warning, then returns to white. A 0.2-second recovery separates warnings, and firing slows if necessary to preserve the complete warning. Actual shots retain their authored warm muzzle flash and smoke. The red charge blob remains removed. Warning/readiness cues reuse the game's assigned audio clips; close passes use the recorded air swish.

## Controls

- PC: press **E** just before a projectile hits.
- The on-screen evade button, timing ring, incoming-shot brackets and dodge text have been removed. Mobile players continue to dodge by steering.
- Steering still avoids projectiles normally. Evading never changes steering input, bike position, physics velocity, or car/wall collision rules.

The default successful input window is the last **0.30 real seconds**; the last **0.12 real seconds** earns a perfect dodge. Early input is rejected, without queuing a later evade. A successful input gives a short upper-body duck/side shift and protects against the selected projectile for that projectile's current pool lease. It does not grant general invulnerability against unrelated shots.

Prediction uses relative projectile/player velocity and the enabled player colliders, and excludes projectiles blocked by nearer obstacles. Readiness is refreshed when input arrives, avoiding stale targets.

A perfect dodge gives a sound, a restrained FOV response and mild slow motion, followed by a smooth recovery. A real last-moment steering escape can also earn the perfect reward. Passing bullets that were always in a safe lane do not qualify. The reward cooldown prevents a burst from repeatedly slowing the game. Existing combat slow motion and sword-strike animations take priority. Pause freezes the clocks.

## Tuning

Select **GameplayManager**, expand **Projectile Combat** under **Enemy shots, muzzle effects and timed evade**. Settings apply live:

- Enemy Trajectory Guides, trajectory width/brightness/opacity/end fade.
- Firing Warning, Enemy Shot Recovery, Enemy Aim Laser, laser width/brightness and white/red colors. The full warning is preserved even if the enemy's old shot interval is shorter.
- Enemy/player muzzle-flash settings, gun smoke and Bullet Air Wake: see GunEffects.md.
- Timed Evade, Warning Horizon, Evade Window, Perfect Window, Recovery, Evade Key.
- Duck Degrees, Twist Degrees, Body Shift, Camera Roll.
- Perfect Slow Motion, Perfect Time Scale, Perfect Slow Duration, Slow Recovery, Reward Cooldown, Perfect FOV Kick.
- Incoming Slow Assist: optional pre-impact slowdown; **off by default**.

These are independent of the existing pistol damage, tap shooting and sword-finisher tuning. Stop and restart Play mode after updating the project to clear the previous runtime HUD. No Android build is included.

## Validation

`ProjectileEvadeChecks.Run` exercises the real Level/player/projectile prefabs in an isolated Unity Editor project. It explicitly selects the legacy laser mode and checks early rejection, blocked-shot filtering, normal and perfect evades, visible pose/recovery, unchanged bike position, ordinary projectile damage without input, pool reset, pause, existing slow-motion priority, last-moment steering rewards, safe-lane rejection, muzzle warning before real firing, and sword-strike priority. Screenshots and results are stored in `ProjectileEvadeValidation`.
