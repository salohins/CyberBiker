The TUNE button at the top left opens a touch-friendly settings overlay during a run or from pause. Riding pauses while it is open. Scroll to see every control; tap a number for direct entry or drag its slider.

Controls:

- Normal speed limit: the existing PlayerController startMaxSpeed field, in the same world units per second as the Inspector and speed HUD. There is no separate starting-velocity setting.
- Forward acceleration, steering acceleration and sideways speed limit.
- Swipe steering sensitivity: the existing TouchInputManager sensitivity scale, 0–19. Higher values make swipes more responsive.
- Launch speed multiplier and duration.
- Player pistol damage and seconds between shots.
- Global enemy follow distance, including new enemies. Finisher-ready enemies still release this gap as before.

APPLY changes the current run and saves overrides on this device. When opened during riding, closing uses the existing resume countdown. Opening from an already paused game leaves it paused on close. CANCEL discards edits. APPLY + RESTART saves and restarts the level. RESET loads the authored scene defaults into the fields; press APPLY to restore them and remove saved overrides.

Settings do not rewrite scene or prefab assets. Untouched installations keep their original values. Saved overrides are read during player initialization on subsequent runs. The menu uses a separate Screen Space Overlay canvas above the game camera's bloom and other post effects, with safe-area anchoring and no keyboard navigation on action buttons/sliders.

Validation: TestingSettingsChecks exercises draft/cancel, live movement configuration, touch sensitivity, global distance, pistol and launch tuning, pause/countdown, persistence through scene restart, reset and preservation of prior pause state. GameplaySmokeCheck also passes with the menu installed. Native render checks cover the upper riding controls and lower combat controls.
