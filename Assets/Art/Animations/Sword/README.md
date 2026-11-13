# Sword animation library

Seven actions from `SourceAssets/Animations/GunAnimations3.blend`, baked at the
original 60 FPS onto all 104 export bones. FBXs contain animation and skeletons
only; existing meshes, materials, gameplay scenes and the player Animator are
not replaced.

| Clip | Source frames | Duration | Loop |
| --- | --- | --- | --- |
| Player_Sword_Grab | 1–35 | 0.567 s | No |
| Player_Sword_Back | 1–35 | 0.567 s | No |
| Player_Sword_Idle | 1–15 | 0.233 s | Yes |
| Player_Sword_Idle_Left | 1–15 | 0.233 s | Yes |
| Player_Sword_Idle_Right | 1–15 | 0.233 s | Yes |
| Player_Sword_Attack_Left | 1–35 | 0.567 s | No |
| Player_Sword_Attack_Right | 1–35 | 0.567 s | No |

## Preview and integration

Use `PlayerSwordPreview.prefab` with `SwordPreview.controller` to preview the
clips on the existing player model. The controller has one state per action;
neutral sword idle is the default. Select an `.anim` and use this prefab as the
preview model, or enter Play mode in a scratch scene and select a controller
state. Gameplay scripts, colliders and audio are disabled on this preview.

Aim pressed while the sword is drawn queues the pistol until the full sword-return clip
and a short recovery finish. This uses 3.5x sword return playback and a 70ms
blend in real time (about a quarter-second handoff, including during slow motion).
Residual boost decays independently rather than blocking Aim. Tune Aim Return
Playback Speed and Aim Return Blend Time on Bike Sword Combat; normal launch
expiry retains its existing animation timing. Releasing or toggling Aim during that transition
cancels the queued request. Both sword prop bones return to their original
mounted local transforms, including after interruption, disable or a crash;
the Humanoid Animator is not relied on to reset these non-humanoid bones.
When Launch is in its mounted riding phase, Aim cancels the boost mode and
starts the pistol draw directly, without playing a redundant sword return.

Pistol Aim uses a 3.5x draw-state speed with a 45ms entry blend and an 80ms
arm-rig blend. The draw still reaches its readiness event before shooting;
target alignment, line of sight, 15 damage and the 0.45s shot interval remain
in force. Draw speed and arm blending use real time during finisher recovery.
Tune Pistol Draw Speed, Pistol Draw Blend Time and Aim Rig Blend Time on
Player Controller. The speed parameter affects the draw state only.

These are **Generic, transform-baked clips**, with `SwordPreviewAvatar.asset`.
This preserves the source's translations, twist bones, jacket and sword
attachment. Unity Humanoid conversion changes this rig's seated pose and drops
some bone motion, so these clips should not be placed directly into the current
Humanoid gameplay Animator. The preview uses a separate Generic Avatar; the
current gun/riding controller and Avatar remain unchanged.

The live player now uses `BikeSwordCombat`: it samples this library on a hidden,
transform-only hierarchy and blends the 104 bone poses onto the existing rider
after animation/IK. It retains the current Humanoid controller, Avatar and
RigBuilder, and blends back to their live riding/aiming pose afterwards.

## Riding combat experiment

Enemy riders must first be staggered with pistol damage. Non-finisher damage
stops above zero health, so repeated pistol shots cannot destroy them. At 30%
health they enter a five-gameplay-second finisher window (five 15-damage hits
against the default 100 health). They stop firing, keep riding, and flash
**blue → orange → blue → orange** throughout the window. Distance does not
select the color. Switching accelerates from 2 to 10 color changes per
gameplay second as the countdown runs out; each new window starts blue and
slow again. Direct palette changes avoid a pale blended intermediate color.
Pause freezes the flashing clock together with the finisher window.

A colored outline, body tint and edge-only emission create the visible halo
while preserving the baked textures. Body tint is alpha-blended and bounded;
HDR emission is confined to the silhouette and narrow rim, instead of being
added across the entire model. Finisher Glow Intensity controls overall strength
(0–1, default 0.9); Finisher Glow Radiance controls the bloom halo (0–1.5,
default 1.1). Both palettes are checked with the production bloom at close and
riding distances. Original materials and global bloom settings remain in use.

Shared combat tuning is on **GameplayManager**. Under **Player Pistol**, set
**Pistol Damage Per Shot** (default15) and **Seconds Between Pistol Shots**
(default0.45; about2.2 shots/sec). These replace the player component's old
weapon tuning fields. Every shot reads the current global values, including
live Play-mode edits; bullets already in flight keep their original damage.
Lower shot intervals fire faster. Enemy gun damage remains on each Enemy.

Healthy enemies share **GameplayManager > Enemy Following > Enemy Distance From
Player** (default20 world units). This is the single global setting for existing
and newly spawned enemies, including during Launch. A value of15 means they
close to approximately15 units, then match the player's speed; editing this one
Inspector field during Play changes every enemy's setpoint. Legacy per-enemy
Distance Controller values are ignored and no longer appear in the Inspector.
The `playerDistance` API reads the global setting. There is no additional
minimum-lead override or captured starting gap. Velocity changes are smooth and
lateral avoidance remains in control. Sword targeting and damage still reject healthy riders.
Staggered enemies are capped at normal bike speed, making them catchable with
Launch. Extra pistol hits do not refresh the window. Pause freezes it and slow
motion stretches its real-time duration. If it expires, the highlight clears,
health recovers to 60%, the enemy resumes firing and becomes uncatchable again. At
the default health/damage settings two more pistol hits reopen the opportunity.
Expiry during draw/dash preparation cancels the pickup safely.

Tune Finisher Health Fraction, Finisher Window, Finisher Recovery Health,
Finisher Speed Multiplier, Blue/Orange Flash Color,
Finisher Flash Start/End Rate, Glow Intensity and Glow Radiance on each Enemy or its prefab. Both enemy
prefabs and all five enemies in Level have the highlight shader assigned.

PC controls: **F** toggles Aim; **Space** toggles Launch; **Esc** pauses.
Right mouse still supports holding Aim. Aim Key is exposed on Touch Input
Manager, and Launch Key on Bike Sword Combat. Space is removed from the legacy
UI Submit binding so a focused menu/HUD button cannot consume a launch press;
Enter and gamepad Submit still work.

Press **LAUNCH** in the Level HUD, or **Space**. The bike boosts with the sword
mounted on the back and the normal riding animation in control. A nearby enemy
triggers a quick 4.5x sword draw (about 0.13 real seconds) just before the dash.
The trigger accounts for relative closing speed so the draw finishes inside the
existing pickup radius (14 world units by default). A short direct lunge goes
at that enemy; there is no long approach into a side slot and no repositioning
of the live enemy. The upper-body slash turns into the lunge direction while the
hips and motorcycle keep their riding pose.

The dash takes about 0.3 real seconds. The full sword action is readable at 0.95x
its authored speed, with an 80 ms hold on the contact pose. The environment slows
for the dash and slows further on the cut; rendering continues at the normal
frame rate. Short tapered wind curls wrap around the rider and bike, while a
separate ribbon follows the blade. Contact adds a sharp burst, sparks, recorded
impact sound, lens punch and a single settling camera kick.

The cut defeats the enemy immediately. Its model receives a forward kick before
being replaced by the existing explosion. That explosion inherits forward
velocity, slows gradually and gets its own short slow-motion window so the bike
can ride through the burst. Passing through the blast also starts a dense,
normal-mapped cartoon smoke wake behind the bike, which rolls and fades for
roughly 1.6 gameplay seconds. Contact with the reserved enemy is suppressed during
the pickup to prevent physical side shoves; walls and other obstacles still
block the dash.

After every slash, the rider immediately plays Sword Back at 3.5x and blends
back to normal riding over 80ms. The sword stays mounted between enemies; each
new pickup draws it again. Boost remains continuous through drawing, attacking,
sheathing and recovery. Its multiplier, rise/fall, seven-gameplay-second launch
window and dash movement settings are unchanged. One launch can chain multiple
passing enemies. At expiry the final sword return finishes and boost eases off.
Press LAUNCH again or request Aim to
cancel. Pause freezes the action and the slow-motion clock; normal clock and
physics timing are restored afterwards. The original Humanoid Animator, Avatar,
RigBuilder and ordinary steering remain in use.

Tune range, dash duration, slash speed, contact hold, boost and forward kick on
**Bike Sword Combat**. Draw Playback Speed, Slice Return Playback Speed and
Slice Return Blend Time control the per-attack equipment cycle. Tune wind ribbon count/width/opacity/flow, blade trail,
impact density, camera punch, smoke wake and the three slow-motion phases on **Sword Finisher
Effects**. Restart Play mode after adding the effects component or changing the
wind ribbon count. `SwordCombatSetup.UpgradeFinisher` binds the owned materials,
impact prefab and calibrated clip contact profiles.

The contact beat widens the FOV by up to 8 degrees, pulls back 1.1m and lifts
the camera 0.35m to frame the rider with the blast. Framing lasts 1.5 real seconds
after the explosion and eases out. The hit also glides an additional 1.25m toward the sword side,
with a 0.18s response and a slower 0.55s return behind the bike. Signed smoothing
prevents snapping between left/right cuts. A capped 12-degree turn frames the
rider and blast from that side. Tune Hit Camera Side Shift, Move Time, Return
Time and Look Turn under Sword Finisher Effects. It retains a 320ms contrast/desaturation
envelope and a single 45ms tapered slash flash at the projected hit position.
The camera owns an isolated image-effect material; normal scene grading and
overlay UI remain untouched. The effect clears on crash, death or disable.
Darkening, desaturation, flash, their durations and hit zoom are individually
tunable on Sword Finisher Effects; set their strengths to zero to disable them.
Recorded swishes lead into layered metal/body impacts. The engine and wind dip
9dB at contact and recover over 320ms. Clips, layer levels and ducking have
separate Inspector settings. The explosion layers a crunchy blast, low-frequency
hit and metallic debris, following the moving explosion in 3D. It has separate
layer volumes and respects the global Explosion volume.

The cut holds 0.20 time scale for 0.55 real seconds. The explosion refreshes the
hold to 0.9 seconds at 0.28 scale, followed by a 0.65-second smooth recovery.
Overlapping phases blend upward; pause freezes the hold and recovery clocks.
Natural launch expiry preserves the final blast; explicit Aim/cancel requests
start recovery. Tune Hit Slow Duration, Explosion Slow Duration, Slow Motion
Recovery, Hit Zoom (negative widens), Aftermath Pullback/Lift/Duration in the
player's Sword Finisher Effects. See Assets/Audio/CyberBiker/CREDITS.md for sources.

Standalone `.anim` paths include the existing player's `Motorcycle/unityRig`
hierarchy. The importer normalizes the FBX's unit-scale skeleton to the existing
rig's 100x wrapper scale and maps trailing dots in right-jacket bone names.
Use these `.anim` files with the preview rig rather than the raw FBX takes.
Root motion is disabled; the player motor remains responsible for movement.

## Launch presentation

The live player has a separate **Bike Launch Effects** component. Launch adds
its recorded high-RPM engine layer and stronger engine/wind mix, localized wind
particles, continuous nitro-style exhaust flames with one spatial ignition sound per activation, +7 degrees of FOV and a
soft blue screen edge. Entry blends in over0.18 real seconds with an entry
pulse; cancellation/expiry fades sustained effects over0.5 seconds.

**Launch Wind Particles** uses an owned copy of the Level scene's existing
`WindParticle` system. It retains stretched
sprite rendering, with the global Wind follow script removed. Launch uses its
own additive, tapered particle material with a saturated-blue emissive core.
Wind Particle Brightness controls glow separately from tint and alpha. Each particle is
independent; particle trails are disabled and no Launch LineRenderers or path
history are created. The global wind object remains unchanged.

Defaults:900 particles/gameplay second,0.24-second lifetime,0.012 width,
0.55 sprite length,10 local units/second backward,0.35 spread radius,100% emitted
behind the rear tire. The rear emitter is centred at(0,0.38,-2) in player-local
coordinates, with narrow horizontal spread and low height to keep the bike clear.
Wind Rear Offset is exposed alongside the spread/rear fraction controls. Tint is saturated blue
(0.015,0.2,1),alpha0.7,brightness3. Particle brightness,density,lifetime,size,
speed,spread,rear fraction and color are exposed in the player's Inspector. Emission, motion and
natural fading continue during Launch; pause freezes the system, Aim/expiry
fade it out, and hit/death clears it. Particles stop before reaching the camera.
During a sword dash emission reduces to40% to leave room for dash effects.

**Launch wind around the jacket** is a separate, smaller particle stream tied
to three animated hem tips: jacket_01_03_end, jacket_01_03.L_end and
jacket_01_03.R_end. Origins are sampled in LateUpdate after the final animated
and sword-layer poses, with a small(0,-0.035,-0.07) player-local offset below
and behind each tip. Emitted particles leave the hem as it flaps and leans.
Defaults:100 particles/sec total,0.18s lifetime,0.008 width,0.4 length,speed8,
brightness2.8 and0.65 opacity multiplier, using the rear wind's blue material.
An additional60 particles/sec flows above the hem at offset(0,0.09,-0.04),
while the lower100 particles/sec remains. Upper emission rate and offset have
their own Inspector controls. All are separate Inspector controls. The approved rear-wheel wind tuning is
preserved. Jacket airflow shares Launch fade,pause,Aim/expiry/hit cleanup; it
does not modify jacket bones or animation settings.

Other Inspector groups control the engine layer/pitch/gain, ignition volume and
continuous flame strength/ember rate, FOV/glow/color/width, and flame size/strength/outlet
coordinates. The three right-hand outlets are measured in Motorcycle-local
coordinates and follow lean. Flames use the existing Cartoon FX Remaster flame
shape and distortion textures, with orange/gold bodies and a blue base.
Nitro remains lit for the full Launch envelope, with a small flicker and entry
pulse. There are no periodic exhaust restarts or repeating ignition pops.
Flames fade with Launch and freeze during pause; Nitro Flame Strength and
Exhaust Ember Rate are exposed. The existing engine power layer sustains the
sound after ignition. Master/engine volumes and pistol/sword/finisher ducking
apply to Launch audio.
The power loop has3D position/reverb; blue borders render before HUD overlays.

**Nitro Ignition Mini Explosion** adds a single five-layer burst at Launch
entry: a short warm flash, expanding blue pressure ring, seven animated Cartoon
FX fire puffs,26 ejecting sparks and three brief smoke puffs. One effect is
centred on the exhaust bank, travels with the motorcycle and then clears,
leaving the continuous flames. It shares gameplay time and pause,and is cleared
on interruption or disable. Existing ignition audio remains one sound per
activation. Overall size is **Ignition Scale** (default1); expand
`NitroIgnition.prefab` to tune each particle layer. Materials and flame artwork
are owned/new or reused from the project's existing Cartoon FX assets.

**Nitro ignition camera impact** adds one short backward kick and upward pitch
when Launch ignites, followed by a damped shake. Defaults:0.18m kick,2-degree
pitch,0.22s kick duration,0.025m/0.75-degree shake for0.32s at16Hz. All are
separate Inspector controls on Bike Launch Effects. The impulse uses real
seconds, freezes during pause and clears on hit/disable; it adds to existing
steering,pass,collision and sword camera channels. It settles to zero without
changing the riding or aiming camera tuning.

`LaunchEffectsSetup.Install` creates/binds the owned particle template and
presentation assets while preserving combat tuning. `LaunchEffectsChecks.Run`
checks individual particle motion, supported glow material and global-style stretched rendering,
rear placement, disabled trails, pause/resume and cleanup, alongside Launch
entry/exhaust/FOV/audio checks. It captures a16-frame motion sequence using
production post-processing. Audio provenance: Assets/Audio/CyberBiker/CREDITS.md.

## Finisher sounds

The enemy plays a ready chime once on entering its five-second finisher window,
then one distinct warning with 1.25 gameplay seconds remaining. Extra damage
replays neither cue and does not extend the timer. Each new window rearms both;
a sword kill or expiry cannot trigger a delayed warning. Pause freezes countdown
and audio; slow motion extends the opportunity while clips keep normal pitch.

Tune **Finisher Warning Lead Time** on Enemy (zero disables the warning; short
windows limit the warning to their last half). On the **CyberBikerAudio** prefab,
**Finisher Notifications** exposes both clips and independent ready/warning
volumes (0.8 / 0.85), plus **Finisher Duck Db** (5). Tactical cues are listener
relative with priority over ordinary effects, so distance does not hide them.
The engine/wind dip holds 120ms and recovers over 300ms, independently of the
pistol and sword envelopes. Kenney CC0 downloaded originals are credited in
Assets/Audio/CyberBiker/CREDITS.md.

## Rebuild and verification

1. Run Blender 5.2+ from the project root:

   ```powershell
   & 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --disable-autoexec SourceAssets/Animations/GunAnimations3.blend --python-exit-code 1 --python tools/animations/export_sword_animations.py
   ```

2. In Unity, select **CyberBiker → Animations → Import and verify sword library**.
   This rebuilds this library and its preview assets, without changing the live
   player. Batch entry point: `SwordAnimationImport.RunBatch`.

The exporter checks every frame against the evaluated Blender skeleton before
writing FBXs. Unity checks all clip bindings, timing, finite poses, stationary
player root, seated hip height, attack movement, and all 104 bone positions at
the start/middle/end of every action against Blender reference poses. It also
checks the sword-to-hand distance and captures preview images.

`SwordAnimations.manifest.json` records source SHA-256, source frame ranges,
loop endpoints, bake accuracy and export sizes. `SourceAssets/Animations/`
retains the original source and pose references outside Unity's automatic
Blender importer. The original file in Downloads is untouched.

The source's unused hand-to-gun parenting constraint has zero influence in all
seven actions. It is muted only in the export working copy to eliminate a
dependency cycle. A missing source HUD font is excluded from the animation-only
exports. No meshes were edited or exported.
