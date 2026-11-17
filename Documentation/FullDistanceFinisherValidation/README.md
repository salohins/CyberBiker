Full-distance, Shoot-triggered finishers

Tap-to-fire Shoot now gives a glowing, reachable enemy priority over a pistol
shot. The current pistol target is preferred when eligible. Otherwise normal
ready-target selection applies. No Launch press or close approach is required.
The configured target range (150 units by default) bounds the dash; nearby-target
priority ranges only affect selection. Solid walls still block commitment.

Each successful sword kill ends Launch immediately. Sword return, moving
explosion, slow motion, traffic protection and recovery still complete. The
waiting enemy is not automatically finished. Repeated taps during the committed
draw or strike are consumed without cancelling the finisher or buffering shots.

Result.txt records native Unity tests with the actual player, enemies, movement,
animations and particle effects: 80m left and 120m right Shoot-triggered kills,
one kill per activation, clean recovery, healthy-target rejection, configured
range, solid walls and expiry during draw. TrafficRegression.txt covers the live
car collision pairs, steering, aftermath protection and restored collisions.
The checks run in the isolated validation project and restore saved TUNE prefs.

PistolRegression.txt records the existing tap-shooting suite: first-shot draw,
rapid taps, no automatic repeat, idle holster, mirrored buttons, pause/resume,
touch/steering separation, pistol balance and legacy continuous aiming. This
test now uses the normal real-time clock to match the draw/holster timers.

No Android APK or physical-device playtest was performed for this change.

Concurrent draw/dash update: the native integration check additionally requires
IsDashing on the Shoot frame, actual forward displacement during Drawing on
both sides, and no Launch activation count or speed boost throughout the cut.
The dash is initialized once at drawing, not restarted on entering Striking.
