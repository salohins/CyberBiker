# Two play modes — native verification

Validated in Unity 6000.6.0f1 using the isolated project copy. The user's
interactive Editor was left open. Result.txt records the native checks.

- The saved Level defaults to Progression and references the preserved old
  Difficulty 4 for Constant Combat; the eight-stage, five-minute timeline and
  final riding/fighting loop retain their original boundaries.
- The real menu Play button opens one selector. Both mode buttons and Back are
  wired; selecting Constant Combat loads the actual Level in that mode.
- Opening riders and replacements all use the real approaching/overtaking
  arrival path. The checks observe their initial distance, intermediate fade
  values and continuous physical movement rather than instant placement in
  fighting slots. The actual unmodified combat prefab is used, including its
  initial delay, spawn interval, 25-unit row-spacing modifier and 2/4/1 traffic
  weights. Defeating the active fighter produces a replacement while the other
  four remain, without waiting for a whole-wave cooldown.
- A missing scene reference is reproduced explicitly and resolves through the
  shared RunModes asset to the old combat preset, rather than the warmup stage.
- Constant Combat holds its chosen difficulty beyond ten simulated minutes;
  pause freezes its clock, and restart preserves the mode.
- Cancelling a TUNE draft does not switch modes; reopening discards that draft.
  Applying Progression restarts cleanly without carrying over combat enemies.
- Real progression starts peacefully and then spawns its original solo rival.
- Applying Constant Combat from that real progression run through TUNE also
  starts through smooth arrivals, with the original traffic settings and a
  five-rider target.

ModeSelection.png and TuneModes.png are native UI captures, rendered after
scene post effects just like the overlay used in the game. Enemy shooting damage
and player movement were controlled only inside the test fixture; arrival
timings and traffic settings use the unmodified combat preset. Fixture
overrides are not saved to game scenes or prefabs. The runner
restores the pre-existing saved tuning preferences on completion.

Android package/build verification is recorded next to the APK under
Builds/Android. No phone playtest was performed by the automated checks.
