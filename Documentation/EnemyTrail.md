Enemy speed trails keep their existing rear anchors and red colour. The new shared shader adds soft edges and a red core; the width and alpha taper toward the tail. Maximum length is limited by adjusting point lifetime against enemy speed. No additional trail renderer or per-enemy material is created.

Tune Enemy (Script) > Speed trail: Lifetime, Max Length, Width and Brightness. Defaults are 0.3 seconds, 18 world units, 0.42 world units and 1.75 respectively. Both enemy prefabs explicitly use these values. The red centre stays visible from the riding camera while the reduced brightness and removal of the white-hot core reduce glare, with feathering at the outer edges. Existing animation and finisher timing settings are unchanged.

The shader fades trail fragments within a capsule around the player, independently of camera angle. When a pickup dash begins, its target's trail immediately stops emitting, clears its existing points and disables rendering. It stays hidden through the strike and death. If the finisher is cancelled and the enemy survives, it resumes with fresh history. Disable/re-enable also clears old points.

EnemyTrailChecks.Run validates shader compilation and actual rendered clearance, normal trail visibility and length, immediate suppression, cancellation recovery and a real finisher kill in an isolated Unity project. It also renders the actual gameplay camera with and without the trail to check that its red light remains visible against the scene. Native captures and the result are in Documentation/EnemyTrailValidation. No phone build was made for this change.

Trail history is cleared with Unity's [TrailRenderer.Clear](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TrailRenderer.Clear.html) rather than waiting for old points to expire.
