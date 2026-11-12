# CyberBiker audio sources and licenses

All active audio assets are downloaded recordings or third-party effects. No generated engine or gun sounds are used. The old idle-only motorcycle loop, laser shots and original pedestrian car pass-by have been removed.

## V12 engine (active)

V12Launch.wav is an excerpt from a Lamborghini V12 recording by cheesepuff, CC0: https://freesound.org/people/cheesepuff/sounds/112075/ . The excerpt is 48.3–56.5 seconds of the source, decoded to mono, level adjusted and faded at the edges. It plays once on launch.

V12Cruise.wav uses a Ferrari F12 2016 onboard engine recording by Pole Position Production from the Sonniss GDC 2020 bundle. Source: https://ftpmirror.your.org/pub/misc/sonniss2020/individual/Pole%20Position%20-%20Ferrari%20F12%202016/Ferrari_F12_t2_Onbrd_Start_Medium_Drive_Stop_Reverse_Stop_Drive_Stop_Off_Engine_Left_DPA4061.wav . License: https://sonniss.com/gdc-bundle-license/ . The 114.6–120.4 second section was mixed to mono, level adjusted and crossfaded with a 0.6-second equal-power overlap into a 5.2-second loop. The two recordings are car engines, not motorcycle or F1 recordings.

The older BMW and Yamaha files remain in the project but are not assigned to the active audio prefab.

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
