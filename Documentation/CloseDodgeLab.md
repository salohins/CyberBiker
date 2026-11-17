# Close-dodge cinematic and Dodge Lab

A hostile bullet that passes within **Pass Radius** of the rider's torso centre starts the cinematic at its closest passing point. The default radius is 1.8 world units and the centre is 1.6 units above the player root. This radius is separate from the existing near-miss sound radius. Direct hits, friendly shots and passes outside the radius do not trigger it. Each projectile lease reports a pass once, and the cooldown prevents repeated slowdowns in a burst. Steering alone can qualify; pressing E is not required. An E-evaded bullet can also trigger it when it actually passes.

The default effect blends the world down to 0.25× speed, holds for 0.5 real seconds and recovers over 0.5 seconds. Its camera zooms in 8 FOV degrees and glides 0.65 units toward the passing side, with a small look turn, roll and short shake. Slow motion and camera movement have independent blend, hold and return timings. Pause freezes the clocks. This is a separate time-scale contribution, so cancellation never cancels a finisher's slow motion. Finishers take priority. It does not steer the bike, teleport the player or grant new invulnerability.

## Open and tune

1. Stop the current Play session, then open **CyberBiker → Dodge Lab**.
2. Click **Start preview**. A real bullet passes the actual riding player in a separate Play run; your open editor scenes return when you stop.
3. Change **Pass offset** (signed metres), **Ride speed**, **Bullet speed**, and **Approach time**, then **Replay**. **Other side** flips the pass. Offsets outside the chosen radius show the no-effect case; a direct collision does not count as a dodge.
4. Edit the settings on the left. They apply to the live preview. Use **Pause**, **Play** and **Step** to inspect timing. **Aim camera** checks the effect while aiming.
5. After a successful pass, scrub **Effect time (real s)** to inspect the post-pass camera and world-speed envelope. This freezes the world; Replay tests the complete bullet approach again.
6. Click **SAVE FOR GAME** at the top. The saved profile is [CloseDodgeTuning.asset](../Assets/Resources/CloseDodgeTuning.asset), assigned to GameplayManager. Future Play sessions and builds load it. Draft edits alone do not change the saved game profile. **Load saved**, **Export** and **Import** support reset and JSON presets.

Settings remain editable before preview, during playback and while paused. Save/preset controls use a separate toolbar row so they fit a narrow dock. The effect timeline becomes available after a successful close pass.

The slow-motion toggle really disables world slowdown; camera motion can remain enabled independently. All timings are real seconds, so shortening world speed does not stretch the recovery timer. The radius is measured from the torso centre, not from the outer edge of the model.

## Validation

`DodgeLabChecks.Run` runs native Unity checks in the isolated validation project. It exercises real pooled projectiles, pass-only timing, radius boundaries, independent sound radius, both camera sides, hit/friendly rejection, E-evaded bullets, pause, cooldown, finisher priority, recovery and disable cleanup. The Lab check covers moving BulletLocal projectiles, live settings, slow-motion toggling, timeline seeking, preset/save roundtrips and restoration of the prior scene/audio/render target. Results and captures are in `CloseDodgeValidation`.
