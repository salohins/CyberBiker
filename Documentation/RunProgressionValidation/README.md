# Native Unity validation

Validated in the isolated RefactorValidation project with Unity 6000.6.0f1. The main Editor session was not controlled.

- `Result.txt`: saved eight-stage configuration; exact five-minute opening; timing boundaries and repeating tail; legacy timing; peaceful start; pause; real solo/pair enemy waves, one-wave budgets and riding breaks; gap assist allowed only while real Launch state is active.
- `LaunchGapChecks.txt`: actual solid-car passes in Launch preview; fixed forward speed; narrow/blocked/out-of-range gaps rejected; steering held through the pass; both approach sides; live camera and particle preview; preset saving preserves assigned assets.

Compilation and these checks passed. Existing missing-script/Visual Scripting serialization warnings remain in the project. Validation fixtures disable damage/collisions while inspecting encounter timing; the gap-pass checks retain solid-car collisions.
