# Enemy traffic investigation

The preserved pre-fix project is on local branch `codex/checkpoint-enemy-navigation-2026-11-13`, commit `286e3ff8e327c4ae6acd8e37f09000dbf8b534ac`. This fix does not move that branch or revert the game.

Two regressions were reproduced in the added navigation layers:

- A private lane waypoint passed the incoming turn-angle/capsule checks but had no legal connection back to the generated traffic network. In the deterministic case, the original next car point was reachable, but the intermediate lane point required a 21.8° onward turn under the saved 15° limit. The traffic layout's spacing guarantee applies to the original connected points; inserting an arbitrary point broke it.
- The added rectangular mesh envelope used a different clearance volume from the planner's radius-1 capsule. It rejected all five tested routes accepted by the original planner. This could pin a rider before its intended turn and then clear its path.

The saved scene reproduced a sustained stall around gameplay time 10.7 seconds. A queued rider at `(8.92, 0, 1209.14)` requested 140 units/second but moved at the car's 20 units/second, with no remaining reachable detour. The checkpoint trace, nearby traffic and dodge points are recorded under `EnemyTrafficInvestigation/Checkpoint`.

The fix preserves the car placement formula, row spacing, 15° turn setting, original reachable-point filtering and random choice among original car points. Private lane points now require a legal onward connection, as well as a legal incoming connection. If future traffic exists but cannot be reached, a short lane point cannot hide that failure. Spawn lanes must also connect to the existing network. The contact safeguard shares `AIController`'s collision radius and uses a sphere sweep, matching the capsule route's clearance instead of imposing a separate rectangular footprint. Drift/lean, particles, sounds, waves and combat tuning are retained.

Validation in the isolated Unity 6000.6.0f1 project:

- Five planner-approved clearance cases: five false blocks before the fix, zero afterward.
- The 21.8° dead-end waypoint is rejected; the original reachable car point remains available.
- Sixty original path-choice comparisons pass unchanged angle, capsule and random-selection rules. Empty-road lane preferences, onward connectivity and spawn-entry rules pass.
- Contact sweeps pass forward, lateral, diagonal, moving/stalled and rotated-car cases at a 10Hz step, with intermediate positions checked against the car's oriented box.
- Native encounter/wave handoffs and front/rear arrival suites pass.
- No sustained stall in a 65-gameplay-second saved-scene run at 60-frame capture cadence, or another at 15-frame cadence. The latter observed five riders, four arrivals and actual Launch mode. This is a simulation cadence check, not a phone performance measurement.

Before/after reports are under `EnemyTrafficInvestigation/Checkpoint` and `EnemyTrafficInvestigation/Fixed`. Restart Play mode to create fresh runtime navigation helpers after script reload.
