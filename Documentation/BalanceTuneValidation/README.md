# Live balance and preset verification

See [the user guide](../BalanceTuning.md) for the controls and export workflow.

- Result.txt: native Unity checks for every numeric balance control, default
  arithmetic, configurable multiplier/repetition rules and hit payout, live
  distance-score rate, actual bullet-pass scoring, persistence across restart,
  Reset, deep draft copies, old saved settings, partial presets, invalid/future/
  oversized JSON rejection, portable JSON/file round trips and profile isolation.
- ScoringRegression.txt: the existing full scoring suite, including real pistol
  hits/kills, sword death, traffic close passes, dodges, banking and board motion.
- ExistingTuneRegression.txt: existing movement/control tuning behavior checked
  with the additional balance fields present.
- Score.png, Triggers.png, Presets.png: actual Unity overlay captures. The values
  shown in the tests are deliberately changed to prove the controls take effect.
- AuthoredDefaults.json: a portable preset exported from the scene/profile defaults.
- ExampleBalance.json: a portable test preset, not a recommended balance.

All Unity checks ran in the isolated project copy. Tests restore the prior saved
tuning preference string, and export tests do not touch the system clipboard or
open file-browser windows. Runtime profile edits use copies so the authored
close-dodge/gap assets retain their camera, effect and trigger defaults.

Android APK build/signature/archive checks are recorded beside the APK in
Builds/Android. The Android Downloads export branch is compiled into the player;
the SDK available here has no emulator image and no physical-device export test
was performed. COPY JSON remains an independent export option in the UI.
