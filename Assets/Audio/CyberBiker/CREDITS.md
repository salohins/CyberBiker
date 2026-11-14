# CyberBiker audio sources and licenses

## Launch mode audio

`LaunchEnginePower.wav`: derived from the existing `R6_Drive_High.wav` recording
by **bodhistone**, CC0-1.0, Yamaha R6 ride recorded on a Tascam DR05x.
Source/license: https://freesound.org/people/bodhistone/sounds/855305/ .
Edits: 180Hz high-pass, 7kHz low-pass, +2dB at 600Hz, 650ms equal-power wrap
crossfade into a 9.35-second loop, mono 44.1kHz PCM, peak -1.51dBFS.
This extra recorded engine layer fades in during Launch at 1.12 pitch; the
existing engine remains audible, with a separate +2dB lift. Wind gains +2.5dB.
It follows the motorcycle in 3D with short reverb and no Doppler, respects
master/engine volumes, pauses, and shares the pistol/sword/finisher mix ducking.

`LaunchExhaustCrack.wav`: **Motorbike Driveby and Backfire** by
**Yoyodaman234**, CC0-1.0.
Source/license: https://freesound.org/people/Yoyodaman234/sounds/667592/ .
Downloaded HQ preview: https://cdn.freesound.org/previews/667/667592_2792951-hq.mp3 .
A real motorcycle accelerating/backfiring, recorded with a BP4025 microphone
and ZOOM F6 recorder in Brisbane. The 74.815-75.635s excerpt contains the strong
backfire transient at74.85s and its engine tail. Edits: mono44.1kHz PCM,75Hz
high-pass/11kHz low-pass,8ms attack fade,180ms end fade,peak0.9. No synthesized
or foley layers. Replaces the earlier JustInvoke backfire-like foley asset.
One spatial ignition clip starts the continuous nitro-style exhaust jets once
per Launch activation; its tail follows the rider in3D. The flame stays lit
through Launch, with the existing recorded power-engine layer continuing
underneath. Ignition does not repeat during the boost or on unpause.
Full provenance: `Licenses/LaunchSources.json`.

## Finisher notifications

`FinisherReady.ogg` and `FinisherWarning.ogg` are unmodified `confirmation_004.ogg`
and `error_003.ogg` from **Kenney Interface Sounds**, CC0.
Source: https://kenney.nl/assets/interface-sounds . License: `Licenses/kenney_ui.txt`.
The ready chime plays once on stagger; the warning plays once with 1.25 gameplay
seconds left by default. Both are listener-relative so distance does not hide
them, play at normal pitch through slow motion, pause with the game and have
separate volumes. Alerts briefly reduce engine/wind by 5 dB (120ms hold, 300ms
recovery). Inspector: CyberBikerAudio / Finisher Notifications; each Enemy has
Finisher Warning Lead Time (zero disables warnings). No health/timer changes.

## Sword cut layers

**artisticdude — Swishes Sound Pack**, CC0. Recorded by swinging a clothes hanger
and sticks of wood; no synthesized sword layer.
Source/license: https://opengameart.org/content/swishes-sound-pack
Download: https://opengameart.org/sites/default/files/swishes.zip
`SwordSwish_01.wav` and `SwordSwish_02.wav` use `swish-9.wav` and `swish-7.wav`:
decoded to mono 44.1kHz 16-bit PCM, 140Hz high-pass, peak limiter at 0.8.

`SwordImpactLow.ogg` is Kenney's unmodified `impactPunch_heavy_000.ogg`, CC0,
from https://kenney.nl/assets/impact-sounds . The cut layers it at 0.82 pitch
with the existing `impactMetal_heavy_001.ogg`, plus the swish timed to the blade.
The impact reduces engine/wind by 9dB for 90ms and restores them over 320ms.
All sword clips, layer volumes and ducking are on the player's Sword Finisher Effects.
The audio pool keeps recorded tails with the rider and respects master/weapon
volumes and pause. Slow motion does not stretch audio into a rumble.

Sword explosions layer Kenney's CC0 `explosionCrunch_003.ogg` (copied unmodified
as `SwordExplosion_Blast.ogg`), the existing `lowFrequency_explosion_000.ogg`
at 0.9 pitch, and `impactPlate_heavy_000.ogg` at 0.85 pitch. Sources:
https://kenney.nl/assets/sci-fi-sounds and https://kenney.nl/assets/impact-sounds .
The layers follow the moving blast with 3D attenuation and no Doppler shift;
their recorded decay plays at normal speed through slow motion. Blast/bass/debris
volumes are separately tunable and also respect the global Explosion volume.
At detonation the riding mix dips for 120ms and restores over 550ms.

All active audio assets are downloaded recordings or third-party effects. No generated engine or gun sounds are used. The old idle-only motorcycle loop, laser shots and original pedestrian car pass-by have been removed.

## V12 engine (active)

V12Launch.wav is an excerpt from a Lamborghini V12 recording by cheesepuff, CC0: https://freesound.org/people/cheesepuff/sounds/112075/ . The excerpt is 48.3–56.5 seconds of the source, decoded to mono, level adjusted and faded at the edges. It plays once on launch.

V12Cruise.wav uses a Ferrari F12 2016 onboard engine recording by Pole Position Production from the Sonniss GDC 2020 bundle. Source: https://ftpmirror.your.org/pub/misc/sonniss2020/individual/Pole%20Position%20-%20Ferrari%20F12%202016/Ferrari_F12_t2_Onbrd_Start_Medium_Drive_Stop_Reverse_Stop_Drive_Stop_Off_Engine_Left_DPA4061.wav . License: https://sonniss.com/gdc-bundle-license/ . The 114.6–120.4 second section was mixed to mono, level adjusted and crossfaded with a 0.6-second equal-power overlap into a 5.2-second loop. The two recordings are car engines, not motorcycle or F1 recordings.

The older BMW files and most Yamaha banks remain unassigned. R6_Drive_Mid.wav is now assigned to enemy arrival engines, as described below.

## Enemy arrival engines and overtakes

Rear arrivals reuse `R6_Drive_Mid.wav`: a 32-second mono bank from **bodhistone**'s Yamaha R6 ride recording, CC0-1.0, recorded on a Tascam DR05x. Source: https://freesound.org/people/bodhistone/sounds/855305/ . Existing source sections and edits are documented in `sources.json`. No new generated engine recording is used. Each arrival gets a pooled moving 3D source, slight throttle pitch variation, restrained physical Doppler, distance attenuation, rear-facing high-frequency damping and short reflections. The engine fades after the rider settles ahead.

The one-shot air rush reuses `TrafficAirWhooshLong.wav` by **DJT4NN3R**, CC0, with the existing edits documented under Passing-air effect. Source: https://freesound.org/people/DJT4NN3R/sounds/449990/ . It plays once at a real rear-to-front crossing near the player and follows the enemy during its tail. No additional Doppler is applied to this recorded sweep. Both sources respect master volume and game pause. Inspector: CyberBikerAudio > Enemy arrivals.

## Pistol shots (active)

**michorvath — 9mm pistol shot**, CC0-1.0.

Source and license: https://freesound.org/people/michorvath/sounds/427592/
Downloaded HQ preview: https://cdn.freesound.org/previews/427/427592_3094998-hq.mp3

Pistol9mm_Recorded.wav is a real 9mm pistol recording, replacing the old Walther PPQ clips in the active audio prefab. Both player and enemy pistol shots use it. Edits: decoded to mono 44.1 kHz 16-bit PCM, trimmed the silence before the attack, retained the recorded decay, base peak adjusted to -2 dBFS, and applied a 40ms end fade. The first 80ms has a +1.5 dB attack boost, easing back to the original gain by 160ms; the final peak is -0.5 dBFS. No synthesized layers, added reverb or compression. Unity imports it as PCM to preserve the attack. The existing runtime pitch variation remains +/-1.5%.

Player shots briefly reduce engine and wind by 6 dB for 55ms, then restore them smoothly over 160ms. Pistol Duck Db, Pistol Duck Hold and Pistol Duck Release are editable on CyberBikerAudio. Set Pistol Duck Db to zero to disable the reduction. Enemy shots do not trigger it. Pistol damage and firing interval are unaffected.

## Older pistol shots (unassigned)

**Ben Jaszczak and The Free Firearm Sound Library team**, CC0.

Primary source and license: https://opengameart.org/content/the-free-firearm-sound-library
Downloaded library mirror: https://github.com/petroulacl/fps-asset-kit/tree/main/sfx/firearm_sfx/Prepared%20SFX%20Library/Walther%20PPQ

Pistol_1.wav and Pistol_2.wav are two actual Walther PPQ pistol discharges from X_31P.wav, at 1.055-1.65s and 5.205-5.86s. Edits: mono, level normalization and short edge fades. Both player and enemy guns use these pistol shots. No rifle or laser samples are assigned.

## Highway pass-by

Car passing by Doppler Highway.wav by **HenKonen**, CC0.

Source: https://freesound.org/people/HenKonen/sounds/682099/
Download: https://cdn.freesound.org/previews/682/682099_10938187-lq.ogg

HighwayPass.wav selects the closest approach at 6.50 seconds, with 80ms of lead-in and 1.2 seconds of departure. Edits: mono, sub-65Hz rumble reduction, level adjustment and short edge fades. Original playback pitch is retained because the recording already includes Doppler. Runtime spatial attenuation follows the passing car.

## Cinematic passing air

TrafficAirWhooshLong.wav uses whoosh_long_low.wav by DJT4NN3R, CC0: https://freesound.org/people/DJT4NN3R/sounds/449990/ . The HQ MP3 preview was decoded to mono, trimmed to 0.65–2.70 seconds, gently compressed, level-adjusted and faded at the edges. The older TrafficAirWhoosh.wav remains in the project but is not assigned to the audio prefab.

## Wind (active)

HighSpeedWindBed.wav and HighSpeedWindRush.wav use Car Strong Wind Noise.mp3 by FunWithSound, CC0: https://freesound.org/people/FunWithSound/sounds/439242/ . The author describes recording wind through a car window while travelling at high speed; this is not a measured 200 km/h helmet recording. The HQ preview was decoded to stereo PCM. The bed uses 12–46 seconds with a 90 Hz high-pass and 11 kHz low-pass; the rush uses 55–89 seconds with a 350 Hz high-pass and 12 kHz low-pass. Both were gain adjusted and use a 1.5-second equal-power overlap for 32.5-second loops. The rush layer rises and pans with steering; it shares the Wind volume control and has its own volume setting.

The earlier outdoor SpeedWindStereo.wav and WindLoop.wav remain unassigned.

## Impacts, explosions and interface

**Kenney**, CC0. Selected Ogg files are unmodified.

- https://kenney.nl/assets/sci-fi-sounds
- https://kenney.nl/assets/impact-sounds
- https://kenney.nl/assets/interface-sounds

Pack notices are in Licenses. CC0 license: https://creativecommons.org/publicdomain/zero/1.0/

sources.json maps the active files to authors, sources, modifications and SHA-256 hashes.

## Jacket fabric

JacketFabricFlutter.wav uses Fabric Flapping by IENBA, CC0: https://freesound.org/people/IENBA/sounds/701647/ . Foley recorded for cape/fabric motion with a Sennheiser MKH50 and Zoom H4n. The HQ preview was decoded to mono; the 0.25–13.25 second section was filtered, given a broad +3 dB presence boost near 1.5 kHz, compressed and level adjusted to help the fabric details stand out in the engine/wind mix, then given a 0.35-second equal-power overlap for a 12.65-second loop. The runtime source is attached to the rider’s animated central jacket hem.

## Pistol hit, damage and projectile near-miss feedback

BulletWhiz.wav is a short mono extract of **Sword or Bullet Swish/Swosh 12.flac** by **qubodup**, CC0 1.0: https://freesound.org/people/qubodup/sounds/59999/ . This is recorded bamboo-stick air movement, not an electronic bullet tone. The downloaded HQ preview is retained as BulletAirSwish_Recorded.mp3. The cue starts 8 ms before the 5%-of-peak onset, lasts 240 ms, has 1 ms/35 ms edge fades and peak normalization to 0.85.

PistolHit.wav uses **body_hit.wav** by **insanity54**, CC0 1.0: https://freesound.org/people/insanity54/sounds/276600/ . A recorded gloved-hand/helmet impact, extracted from 4 ms before the 5%-of-peak onset for 200 ms, with 1 ms/35 ms edge fades and peak normalization to 0.94. The HQ preview is retained as PistolHit_Recorded.mp3. This replaces the UI click as the confirmed-damage cue.

License, download URLs and SHA-256 hashes are in Licenses/GunFeedbackSources.json. Incoming-damage thud still reuses Kenney's impactPlate_heavy_000.ogg, already credited above. The existing pistol shot recording and its mix ducking remain assigned.
