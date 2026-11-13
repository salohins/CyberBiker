Pooling, AI physics and off-track recovery — 13 November2026

Road chunks, traffic cars, projectiles and player muzzle flashes now reuse matching prefab instances. PrefabPool is owned by the scene and keeps bounded inactive caches. Fresh instances are created on demand when a matching free instance is unavailable; active gameplay demand is never capped. Exiting/unloading the scene destroys its cache and removes the static scope entry.

| Family | Inactive cap per prefab | Inactive cap per scene |
| --- | ---: | ---: |
| Projectiles |64 |96 |
| Muzzle flashes |4 |16 |
| Traffic cars |4 |32 |
| Road chunks |2 |8 |

On reuse, child poses/activation, original layers, collider/trigger flags, renderer/behaviour flags and particle/trail state are restored. Explicit adapters reset car health, speed and collision state, projectile ownership/damage/inherited velocity/lifetime, and a tile's collectible flags. Old car explosions are cleared with the old car; detached self-managed effects keep their own lifetime. Tracer children retain their renderer instead of auto-destroying it while cached. Muzzle flashes retain the existing three-frame visibility window, including concurrent requests.

AI waypoints store a pool generation token. A retired car's waypoint becomes invalid immediately and cannot be revived when the same object is moved to a new spawn. Traffic cleanup and wave planning skip invalid points. Car return also removes pass-crossing history and detaches recorded audio tails at their final position, avoiding false whooshes and teleporting sound tails.

Runtime AI physics retains its decision/update frequency, capsule radius, angle limit, candidate order and random selection. It caches masks/point positions, reuses the enemy's path-query root and avoids inserting/removing that root in the shared navigation list. Obstacle overlap queries use a two-collider NonAlloc buffer: the original rule treats any count of two or more as blocked, so saturation cannot turn a blocked gap into a free gap.

Measured desktop Editor CPU cycles:

| Local CPU cycle | Before (ms) | After (ms) | Less time |
| --- | ---: | ---: | ---: |
| Projectile create/initialize/retire | 0.0490 | 0.0288 | 41.3% |
| Traffic car create/initialize/retire | 0.1965 | 0.0366 | 81.4% |
| Road tile create/retire | 0.1024 | 0.0280 | 72.7% |
| Muzzle flash create/retire | 0.0376 | 0.0145 | 61.5% |
| AI path query with reusable/fresh root | 0.0152 | 0.0064 | 57.8% |

These are isolated Unity6000.6.0f1 Windows Editor measurements, not whole-game FPS. Baseline cycles explicitly Instantiate/initialize/DestroyImmediate; pooled cycles rent/reset/return warmed instances. Four warm-up cycles precede100 projectile cycles,50 car cycles,8 tile cycles and100 flash cycles. AI measurements use500 path queries with controlled geometry. Immediate destruction includes destruction work rather than its usual deferred frame placement. Small-sample timing and Editor overhead limit extrapolation. Source and raw measurements are retained in baseline.json and pooled-final.json.

Off-track investigation and fix:

The user's live Unity scene was inspected while paused. enemy1(Clone) was at X=-62.553,Y=0,Z approximately6998.45, heading-1.089 degrees, still Spawn, with a follow target approximately(-62.811,0,7012). No live transforms or runtime values were edited; the Inspector was returned to Normal mode.

The no-route fallback pointed toward the enemy's own forward probe, retaining lateral heading indefinitely. A controlled pre-fix reproduction drifted29.782m sideways in2 seconds. The corrected fallback aligns with the road and smoothly recovers toward its lane corridor. Behind/invalid route targets are rejected. Cached lane metadata comes from the active road tiles and is invalidated by tile reuse, without adding recurring road raycasts. Corridor correction only reduces the proposed lateral movement; it cannot teleport an escaped enemy into a lane.

A passed spawn-state enemy resumes distance control so it can catch up, while retaining the original70m activation range for attacks. Finisher-ready speed rules and sword death motion are preserved. The captured escaped position is covered by EnemyRoadChecks alongside the no-route case.

Validation evidence:

- PoolingPhysicsChecks:60 seeded path choices match the original physics/angle/selection rules at0/45/90-degree limits; shared navigation list preserved.
- PoolLifecycleChecks: projectile damage once, new-owner speed/damage/collision resets, old-lease expiry protection, scaled-time lifetime/pause, tracer clearing, car health/collider/renderer reset, waypoint/audio invalidation, bounded cache with100 active shots, three-frame muzzle reuse and scene disposal.
- Legacy Instantiate, fresh pooled and reused road chunks render identically in the controlled320×200 comparison (zero changed bytes between fresh/reused, legacy comparison also passed). Images: tile-legacy.png, tile-fresh.png, tile-reused.png.
- Continuous streaming:24 road shifts preserve compatible, gap-free joins; traffic reaches its full configured buffer after advancing;44 reuses recorded during this scenario. Initial distant spawn placement is allowed to prime during travel, as in the real game; density, spacing and buffer settings were preserved.
- EnemyRoad-baseline.txt and EnemyRoad-fixed.txt record drift and recovery. Gameplay and finisher regression reports are stored alongside this audit after completion.

The pass preserves gameplay values, traffic selection/layout rules, player movement/camera/lean settings, visual assets and the production60 FPS cap. Fresh Play mode initializes the new runtime caches. Remaining candidates include lane-selection temporary collections/GUID shuffling, small managed health-state allocations and standalone short-lived impact/explosion effects; those require their own measurements and reset policies.

Final verification: PoolingPhysicsChecks + PoolLifecycleChecks, EnemyRoadChecks, EnemyFinisherChecks and GameplaySmokeCheck all returned exit0. Latest drift test:0.842m with heading0, versus29.782m baseline. Legacy/fresh/reused tile PNG SHA-256 values are identical. Final runtime and validation source bytes match the isolated project. Logs are in Logs/sword_import/pooling-*.log and enemy-road-*.log.
