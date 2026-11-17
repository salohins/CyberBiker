Predictive side entry for gap assist

Before.txt records a missed countersteering approach with zero gap activations.
The old filter required the bike already to be inside the snap radius and treated
opposing input as moving away, even while actual lateral velocity was toward the
gap. A short forward limit could also make the safe acquisition window smaller
than a physics step at high speed.

The assist projects lateral velocity over the approach horizon and recognizes an
inward-moving bike that will enter or cross the catch region. Countersteering
does not veto this actual inward trajectory. The configured maximum forward catch
distance is a firm limit: prediction cannot extend it or take steering farther
away. Clear-path, bike-width, bumper-entry, Launch and collision checks remain in
force, as does steering lock through a committed pass. Entries that cannot align
safely inside that limit are rejected instead of starting early.

The existing snap radius, base forward distance, camera and VFX values are retained.
Predictive side entry is enabled by default and exposed in TUNE > TRIGGERS >
GAP ASSIST — LAUNCH ONLY. It exports/imports with the balance preset. Disabling
it disables lateral trajectory prediction while preserving the forward limit.

Native checks cover countersteering, inward movement, neutral corrections and
already-banked/yawed approaches from both sides at 65 and 110m/s, centring before
the bumpers, solid cars, no penetration/damage, no forward boost, and rejection of
blocked, narrow and too-late entries. The TUNE control, a false-value preset round
trip, and defaults for older gap JSON are also checked. The broad side-entry
cases explicitly use a 65m fixture limit. Additional 20m boundary checks prove no
control/camera change beyond the limit or at 20.3m, safe predictive countersteering
capture at 19.7m, and rejection of an impossible high-speed correction inside
20m. Every actual pass checks its recorded entry distance against its limit.
