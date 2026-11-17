# Native scoring validation

Unity 6000.6.0f1, isolated RefactorValidation project. The main Editor session was not controlled.

`Result.txt` covers combo arithmetic, variety/repetition, multiplier cap, exact-once banking, lost pending points, real confirmed pistol damage, takedowns, sword death, projectile near misses, perfect-dodge bonus, actual car/bike close-pass geometry, pause, run-total integration, score sounds, non-interactive HUD graphics and reduced motion. Floating popup content is asserted to match only a plus sign and digits.

`Combo.png` and `Banked.png` are native camera captures with the score canvas composited as an overlay. They show the approved original compact layout with small cyan broken-line/chevron accents and numeric-only floating rewards. Combat actions are deliberately triggered together to inspect busy feedback.

The existing gap, tap-pistol and Tune-settings integration suites are also run after scoring integration. The gap suite includes an assertion that a completed real, solid-car gap pass earns its style reward.

Existing missing-script / Visual Scripting serialization warnings remain outside this change. These tests do not measure phone performance; scoring uses fixed UI pools, the current pooled audio mixer and a 20 Hz bounds-based traffic scan.

## Cyan multiplier, timer and numeric font

`CyanCombo.png`, `CyanBanked.png` and `CyanChecks.txt` validate the compact cyan outline multiplier, sixteen-cell timer and Chintzy CPU floating-number typeface. Assertions cover unchanged badge bounds, timer decoration staying inside its original width, real visible charge, all charge cells empty after banking, the correct number font, non-interactive labels and numeric-only popup content. The existing scoring integration checks also pass. The main score font and layout stay as before.
