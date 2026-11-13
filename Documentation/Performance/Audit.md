Code performance audit — 13 November 2026

This pass targets recurring CPU work and managed allocations while preserving the current handling, combat values, traffic density, audio and visuals. Production still requests60 FPS; only the isolated benchmark uncaps it.

Implemented changes:

- Car and Enemy lifetime registries replace whole-scene searches in traffic audio, pistol targeting and sword targeting. Call sites retain active-object/living-target filters. Disabled movement components remain available to the existing sound/targeting behavior. Creation, reactivation without duplicate entries and destruction cleanup are checked.
- The pistol applies the live global interval, then checks cooldown before doing its aiming raycast. ProjectileWeapon retains the final cooldown guard. Live damage/fire-rate tuning remains intact.
- Repeated target searches reuse the current enemy's torso Transform instead of allocating another hierarchy array for an unchanged target.
- Cars cache their colliders and wall layer mask. Dead, passed and vertically irrelevant cars are rejected before wall raycasts. Their movement equations are unchanged.
- Enemy movement reuses its cached PlayerController.
- Traffic cleanup uses indexed loops instead of per-frame predicates/LINQ Max enumeration; spawn rules and lane selection are preserved.
- Score text changes only when the displayed integer changes. Speed/difficulty use TMP numeric formatting, gated by their displayed two-decimal value. This avoids both temporary formatting strings and repeated canvas dirtiness for an unchanged visible number.
- An empty sword trail mesh is no longer rebuilt/uploaded every idle frame. An inactive finisher highlight no longer rewrites every overlay renderer's enabled flag every frame. Existing trails and highlight appearance remain intact.

Measured fixed-view comparison (one paired Editor run; figures are local observations):

| CPU function | Before (microseconds/call) | After | Less time |
| --- | ---: | ---: | ---: |
| Traffic audio scan | 20.359 | 2.776 | 86.4% |
| Pistol target acquisition | 21.858 | 3.895 | 82.2% |
| Pistol attempt during cooldown | 1.026 | 0.027 | 97.4% |

| Fixed-view mode | Before median frame (ms) | After median frame (ms) | Before FPS | After FPS |
| --- | ---: | ---: | ---: | ---: |
| Riding | 3.593 | 2.460 | 247.6 | 383.0 |
| Aiming | 5.673 | 3.382 | 158.0 | 287.5 |
| Launch | 4.079 | 2.736 | 238.4 | 350.3 |

The sampled FPS includes Editor/rendering work and sampling overhead. These CPU-function reductions do not imply a matching multiplier in game FPS. Raw data: baseline-fixed.json and optimized-fixed.json.

Measurement method:

RuntimePerformanceChecks.Run runs in an isolated copy using Unity6000.6.0f1 on Windows/D3D11. It uses eight cars, two enemies, zero steering and nonlethal fixture pistol damage; player collision is disabled only in this test. Fixed actor anchors and a fixed follow-camera pose hold the rendered scene steady across the comparison. Normal game scripts, animation, audio and physics continue running. It renders960×540 HDR frames explicitly, warms each mode, then samples90 frames each for riding, aiming and Launch. Microbenchmarks warm their calls before timing200 scans/acquisitions or1000 cooldown attempts. Registry lifecycle probes and microbenchmarks execute after frame sampling.

Frame samples describe a stationary Editor fixture, not a traveling game or a phone. Editor initialization, indexing, rendering and background work still contribute. The earlier moving-camera exploratory runs varied too much to establish an FPS gain; they remain in Logs/RefactorValidation/Logs/Performance for inspection. The paired fixed-view files are the report's final comparison.

GC.GetAllocatedBytesForCurrentThread was unavailable in this Unity Mono run: bytesPerCall=-1 means unavailable, not zero allocation. Frame GC counters also include Editor work; this pass does not claim zero-GC gameplay. The rider-ray microbenchmark measures already-baked mesh queries, excluding that frame's skin baking cost; the exact visibility raycaster was preserved.

Validation:

- GameplaySmokeCheck passed movement, actual Aim/fire, live global damage/rate changes, cooldown rejection/acceptance, traffic buffer/refill across1200m, collisions, pass-by selection/audio/reverb/shake, pause/resume, damage and death/recovery guards. Report: GameplayChecks.txt.
- EnemyFinisherChecks passed global distance changes for multiple enemies, real pistol stagger, nonlethal damage, window expiry, warning sounds, healthy-target rejection and sword kill/explosion/expiry races. Report: FinisherChecks.txt.
- The profiling run checks that gameplay remains alive/Playing, exact rider rays produce real hits and actor registries correctly handle creation, reactivation and destruction.
- Shared runtime source bytes match the isolated validated copy. User scenes, materials and gameplay tuning were preserved in this pass.

Remaining opportunities require further measurement. Bullets, muzzle flashes, traffic and short-lived impact effects still use Instantiate/Destroy and could benefit from pooling with correct state reset. Lane selection still uses GUID/LINQ shuffling on spawn. AI path retries still allocate DodgePoints and issue capsule/overlap queries. Audio filter/voice updates and full skin-baking cost also merit isolated profiling. This is a measured optimization pass; best practices are not yet applied consistently to every process.


Follow-up: road/traffic/projectile/muzzle pooling, AI query reuse and an independently reproduced off-track fallback fix are documented in [Pooling/Audit.md](Pooling/Audit.md). The remaining-opportunity list above records the state of the first pass.
