# Style scoring

The game now rewards stylish driving and combat with a separate pending combo, animated score/multiplier display, move feed, pooled world-space numbers and score sounds. Banked combos enter the existing run total and game-over score. The original distance score remains.

## Rules

- A qualifying move adds base points and refreshes a **5-second gameplay-time combo window**.
- Every four multiplier steps raises the multiplier, up to **x10**. A different move adds two steps; repeating the same move adds one.
- Consecutive repeats are worth 72% of the previous repeat's value, with a 25% minimum. This rewards mixing driving, shooting and dodging.
- Five seconds without another move banks **base points × multiplier** once. Example: a 40-point pistol hit, a 120-point close shave and an 800-point sword finisher produce **960 × 2 = 1,920 points**.
- Taking damage, a physical collision kick, falling/crashing or game over **banks half the current base points × multiplier**, rounded down to whole points, then resets the combo and multiplier. Previously banked score stays untouched. Repeated notifications for the same impact cannot bank or halve the points twice. Normal quiet-time completion still pays the full amount.
- Pause freezes the combo. Finisher traffic protection holds its timer, and slow motion uses gameplay time.

## Default moves

| Move | Base points | Qualification |
| --- | ---: | --- |
| On Target | 40 | Confirmed enemy damage from a real player projectile; firing into the air gives nothing. |
| Takedown | 350 | Pistol kill, once per enemy. The final confirmed hit can also earn its normal hit points. |
| Sword Finisher | 800 | Successful existing sword defeat, once per enemy. |
| Bullet Dodge | 150 | Existing per-projectile close-pass detection; bullets that hit the player do not qualify. |
| Perfect Dodge | 350 | Existing perfect-dodge event; this is a bonus in addition to a later qualifying close pass. |
| Close Shave | 120 | Overtake a live car with 0.03–1.2 units of physical side clearance at driving speed. |
| Razor Close | 240 | The same near miss with less than 0.35 units clearance. |
| Thread the Needle | 300 | Complete the real Launch-only gap assist, with the whole bike clear. |
| Full Throttle | 100 | Eight clean grounded seconds above 90% of the normal speed limit. The interval stays longer than the combo window, so straight riding alone cannot maintain an endless chain. |
| Hard Carve | 75 | Hold at least 75% steering for 0.45 seconds at driving speed. Alternate sides to score another; two-second cooldown. |

Driving bonuses require at least 25 units/second. Close passes use the actual car and player collision bounds, track pool generation, and cannot repeatedly score once the same car is behind. Collision recovery blocks driving rewards briefly. Finisher movement does not generate fake traffic near misses.

## Tuning

Select **GameplayManager → Style Scoring** in the Inspector. Tune each move's points, combo window, multiplier cap, repeated-move value, driving thresholds, HUD position/scale, world popups, motion intensity and sound volume. Edit outside Play mode to keep Inspector changes.

The game's **TUNE → SCORE** tab exposes all scoring and combo balance values, including hit payout, repetition limits and multiplier growth. **TUNE → TRIGGERS** exposes their driving/dodge/gap/finisher thresholds and cooldowns. Expand section headers to find the controls. Apply saves on the device; PRESETS exports/imports portable JSON. See [BalanceTuning.md](BalanceTuning.md) for the complete workflow. Animation intensity **0** is the reduced-motion mode: immediate score changes, stationary popups and no score bounce, rotation or celebration shards. The Inspector also has an explicit Reduced Motion switch.

The HUD retains the original compact slanted dark plate and layout. The multiplier now has a thin cyan clipped-corner outline and dark holographic backing, and the timer uses sixteen cyan charge segments with small arrow end caps. Their positions and compact footprint remain fixed. Floating +numbers use the existing angular **Chintzy CPU** typeface, separately from the main score and move labels. Change **Popup Typeface** in the Inspector to try another font. Only **+numbers** float near gameplay; move names stay in the fixed action feed. The HUD respects the screen safe area and ignores all input. The fixed HUD uses a dark backing and outlined text without world bloom. The floating numbers are actual world-space canvases: hits follow the enemy, while driving and dodge rewards travel beside the bike. After a defeated enemy disappears, its numbers continue with its captured velocity. They face the camera, rise a little and fade, with no sideways scatter, spin or bounce. Scene geometry can occlude them. Simultaneous rewards stack, and rapid repeated hits can combine into one number. The counter shows base points next to the multiplier, then the final multiplied value when banked.

**GameplayManager → Style Scoring → 3D Score Numbers** exposes lifetime, scale, settled brightness, appearance brightness, glow settle time, background opacity, near distance, rise and world offset. Each number has a compact slanted dark navy backing fitted to its digits; the backing fades and moves with the number. Background Opacity defaults to **0.96** and can be set to zero to hide it. Brightness starts at **0.94**, settles over **0.22 real seconds** to **0.82**, and stays at the settled value in reduced-motion mode. The opaque dark backing has only 11 pixels of horizontal padding and a 64-pixel height at the 64-point font size. Car close-pass awards appear halfway between the closest collision-bound surfaces of the bike and car, lifted above the vehicle bounds for readability, then travel with the bike. Near Distance defaults to **16** and shrinks close numbers so they do not fill the view. The global bloom profile is unchanged. Restart Play mode after loading the code changes.

The active local experiment uses four original Carbon PC drift/interface cues at their original pitch. **CyberBikerAudio → Style Score Sounds → Use Carbon Score Prototype** loads the separate local reference set on the next Play when its four files are present. Score point, multiplier, bank and loss gains remain individually editable; the existing GameplayManager/TUNE volume still applies. Ordinary ticks keep their rate limit, and multiplier/bank rewards retain their priority and brief engine/wind duck. The rejected Hove impacts and Eric click layers are unassigned. Carbon files are proprietary prototype references ignored by Git, not licensed release assets. With the prototype switch disabled or local files absent, the separately assigned CC0 WobbleBoxx clips remain the fallback. Details and source mappings: Documentation/CarbonScorePrototype.md.

## Compact board reactions

The compact plate, font and layout remain unchanged. Confirmed points now kick a damped score spring; repeated taps add momentum instead of restarting a tween. Driving and combat rewards produce stronger reactions than ordinary pistol hits. Multiplier gains snap the cyan badge, release two short outline echoes and send an energy sweep across the dark plate. The timer refills smoothly with a bright leading segment, and high multipliers strengthen the short reward streaks without a permanent flashing loop.

Banking counts from the displayed base points to the exact multiplied total over 0.32 seconds. The earned multiplier moves toward the score and fades as the total settles, with a brief gold payoff and twelve reusable shards. Damage and collisions also trigger banking, never the loss animation. Pause freezes these reactions; Reduced Motion or animation intensity zero removes the springs, sweeps and echoes and displays the banked amount immediately.

**GameplayManager → Style Scoring → Score Board Motion** exposes Score Punch, Multiplier Punch, Sweep Strength, Bank Count Seconds and Combo Energy. The overall animation intensity in TUNE still scales the effects. Change Inspector values outside Play to save them with the scene/prefab.

Score audio now separates ordinary hits, driving bonuses and combat rewards. Close shaves, bullet dodges, gap passes and driving rewards use the approved short Carbon cue at a stronger gain than routine hits. Kills, finishers and perfect dodges use the stronger reward cue, while multiplier gains, banking and combo loss keep their dedicated roles. A final-hit tick is stopped when a larger reward arrives in the same rendered frame, and major rewards bypass the routine tick rate limit. The runtime audio rig exposes separate Driving Bonus Volume and Combat Bonus Volume controls. This adds no scraping layers and keeps the Carbon recordings at their original pitch.

Native animation preview and checks: [ScoreBoardValidation](ScoreBoardValidation/README.md).

## Structure and performance

`StyleCombo` owns arithmetic, pending points and bank/break events. `StyleScoreController` qualifies gameplay events, samples nearby traffic at 20 Hz and credits `RunSession`. `StyleScoreHud` owns the compact overlay and twelve pooled celebration pieces. `WorldStyleNumbers` owns eight reusable world-space canvases and numeric labels, with no raycast targets or per-award instantiation/destruction. Scoring changes no damage, steering, speed limits, hit boxes or enemy AI.
