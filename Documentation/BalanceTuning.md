# Balance the game in TUNE

Restart Play mode in Unity after updating, or install the new APK. Open TUNE.
The game pauses while the menu is open. Tap section headers to expand them.

- **RIDE:** existing controls, movement, pistol damage, launch tuning, enemy
  distance and play-mode selection.
- **SCORE:** distance-score rate; points for all nine actions; combo duration, cap and multiplier
  steps; different/repeated-move steps; repetition reduction and its floor;
  minimum awarded points; hit payout fraction; holding the timer during
  finishers; score animation intensity and sound volume.
- **TRIGGERS:** driving speed requirements, carve threshold/hold/cooldown and
  alternating-side rule; speed-bonus threshold/interval and passive-chain
  prevention; driving-reward lockout after a hit; car close-shave clearances and
  razor-close bonus; bullet scoring radius/centre; close-dodge cinematic
  radius/centre/cooldown/delay; evade/perfect windows and reward cooldown;
  enemy shot-warning timing and helmet-warning range; Launch-only gap assist
  ranges; sword full-dash range, nearby-target preference, vertical reach,
  stop distance and cooldowns. Launch immediately prepares a dash to a glowing
  enemy within the full-dash range (150 units by default), without needing to
  ride into the nearby-target preference range. One successful sword kill ends
  Launch; its sword return, explosion, slow motion and traffic recovery still finish.
  In tap-to-fire mode, the next Shoot tap can start this finisher directly when a
  glowing target is available; otherwise it remains a pistol shot.
- **PRESETS:** export or import the complete Ride/Score/Triggers draft as JSON.

RIDE also contains the score-powered Launch bar controls: full-bar duration,
score needed to refill, minimum activation charge, and starting charge for a new
run. These export with presets. See [Launch energy](LaunchEnergy.md).

Every numeric control has both a slider and a typed value. Most distance values
use world units. Labels distinguish game seconds from real seconds. Fractions
use 0–1: **Hit payout 0.5** banks half the multiplied pending combo when hit;
**1** banks all of it, **0** banks none. Existing banked score stays intact.
**Speed threshold 0.9** means 90% of the normal speed limit. Distance Points Per
World Unit defaults to **0.01**, preserving one point per 100 units travelled;
set it to zero to disable that separate base score. Style Scoring Enabled
controls the combo rewards separately.

## Apply, cancel and reset

Editing changes the draft. **APPLY** updates the running game and saves the
settings on that device; the new values survive scene restarts and app launches.
Changing play mode restarts the run, as before. Existing pending base points are
not rescored retroactively: new rewards use the new points, while the active
multiplier is recalculated from the configured steps/cap and the remaining
combo timer is clamped to the new window.

**CANCEL** discards the draft. **RESET** puts the authored scene/profile defaults
into the draft; press APPLY to use them and remove saved overrides. Old saved
settings from before these controls were added retain the original balance
values for their missing fields.

Bullet scoring and the close-dodge cinematic have **separate** radii and centres.
Car close-shave clearance measures the gap between collider sides. Perfect
Window is constrained to fit within Evade Window. A zero-point repeat does not
refresh the combo when Minimum Points is also zero. With Prevent Passive Speed
Combos enabled, the speed-reward interval is at least the combo window plus the
configured extra gap. Lab previews can temporarily override their own camera/
profile values; use normal Play mode to judge TUNE balance changes.

Applying close-dodge and gap triggers changes runtime copies. It does not alter
the source ScriptableObject profiles or camera/effect settings.

## Export and move a preset

1. Open **TUNE → PRESETS → EXPORT FILE**. This exports the current visible
   draft, even if it has not been applied yet.
2. In the Unity Editor, JSON files are written to the project's **BalancePresets**
   folder. Standalone PC builds use their app-data **BalancePresets** folder.
   **OPEN EXPORT FOLDER** opens the location on PC.
3. Android 10+ saves a JSON file in **Downloads/CyberBiker**, without asking for
   broad storage access. A local app backup is also written. Android 8–9 uses
   the app's external-files **BalancePresets** folder; the menu displays its path.
4. **COPY JSON** is an alternative for pasting the preset into chat or another
   device. The game does not send it anywhere automatically.

To import, copy the JSON file's contents, open **TUNE → PRESETS**, press **PASTE**
and **LOAD DRAFT**. Go back to TUNE, review the values and press APPLY. Malformed,
unsupported-version and oversized presets are rejected without applying them.
Valid imported values are clamped to supported ranges. Omitted fields retain
the current/default values. Presets contain portable values, not font/material
references or Unity object IDs. Play mode stays separate so a preset cannot
silently switch the chosen mode.

The versioned envelope is `format: CyberBikerBalance`, `schemaVersion: 1`, with
the complete `settings` payload. A native exported defaults file and test preset
are included in **Documentation/BalanceTuneValidation**. The test preset contains
deliberately changed values for verification and is not a recommended balance.

## Validation

Native Unity checks cover every numeric control, draft/cancel/reset behavior,
old saved preferences, persistence across restarts, JSON/file round trips,
partial presets, invalid imports, actual bullet-pass scoring at the selected
radius, actual hit payout and unchanged authored profiles. The full scoring
integration suite also passes with default values. Android package validation
is separate from a physical-phone test; these automated runs did not include a phone test.

Android's export uses the owned Downloads collection and pending-write flow
described in the [Android shared-storage documentation](https://developer.android.com/training/data-storage/shared/media).
