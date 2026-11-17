Open CyberBiker > Finisher Lab. Stop and restart an existing Lab preview once after a code update. Your draft sliders are kept.

The fixed toolbar at the top contains Save for game, Reload saved defaults, Export preset and Import preset.

Open **Dash camera** for the independent finisher approach view. This group opens
by default. **Swing & impact camera** contains the existing swing, hit and
explosion controls.

| Dash camera setting | Effect |
|---|---|
| Enable dash camera | Enables this camera layer independently of the finisher. |
| FOV offset (+ = wider) | Added to the normal FOV; positive widens and negative zooms in. The final FOV retains the 38–110 degree guard. |
| Position X / Y / Z | Camera-local sideways, height and forward offsets. Negative Z pulls back. |
| Rotation pitch / yaw / roll | Independent rotation offsets in degrees. |
| Mirror X/yaw/roll for side | Mirrors those axes for left/right cuts. Turn off for a fixed camera side. |
| Target framing | Maximum turn toward the enemy; set zero for only the chosen rotation. |
| Blend into / out of dash | Transition durations in real seconds, independent of world slow motion. |

This view starts with the draw/approach and blends into the swing after drawing
and the physical dash are complete. Existing swing/impact sliders do not control
the dash view. The Lab uses the direct Shoot-triggered finisher, so manual
Launch/nitro camera motion does not contaminate the preview. The Lab uses
the saved **Combat Riding** view from Camera Lab as the base, pinned throughout
the preview and explosion. Restart or Replay after changing the saved Camera Lab
profile. Recorded takes include this combat view from their first frame, with
the finisher's dash, swing and impact camera layers applied over it. Camera edits update
the live preview; paused FOV, position and
rotation edits refresh without advancing the rider, damage or particles. Use
Replay to test transition timing, and Record a new full 3D take to update a scrubbed
recording. Save for game and the Lab JSON preset include all dash controls.
Older drafts migrate using their existing swing offsets without resetting them.

- Replay tests the current draft on a fresh target. Left/Right chooses the enemy side.
- Save for game writes only the Lab's animation, finisher camera, particle and sound tuning fields to Player_Last.prefab. Future runs and builds use those values.
- Export preset writes a separate JSON settings file. Import preset loads one into the draft. Neither button exports a recorded take.
- Reload saved defaults discards draft edits and reads the player's currently saved tuning.

World slow motion is a master switch for the dash, hit and explosion world-time effects. Turning it off immediately cancels any active finisher slow motion. It does not change animation playback speed, the impact-pose hold, or the dash duration. For example, Swing speed 0.1× still makes the sword animation ten times slower. Animation 1× resets draw, swing and sheathe playback speeds to authored speed while keeping transition settings.

To inspect the complete finisher, press Record full 3D take. The Lab starts a fresh sequence and captures the actual blended poses, particle playback state and trails, sword ribbon geometry, materials, lights, impact flash and game camera. Capture uses fixed steps for the finisher's real-time clock as well as Unity's scaled clock; ordinary gameplay and player builds keep their normal clocks. Recording is silent so audio DSP timing cannot drift against offline capture.

When recording finishes, the live run pauses. Use Whole finisher (s) to seek backward or forward, or use the frame buttons. Jump buttons select Drawing, Striking, Sheathing, Recovering, Hit or Explosion. Playback speed changes only the viewing speed of the recorded take. The recorded world-speed display follows the selected frame.

Drag the recorded image to orbit. Middle-drag pans; the wheel zooms. Front/Left/Right/Back choose fixed views; Game camera returns to the recorded cinematic view. Orbiting never changes gameplay camera settings or advances the particles. These are 3D snapshots, so the enemy and effects can be inspected at earlier times even after their original objects have been destroyed.

Changing tuning marks a take as stale. Record again to include the edits. Takes are temporary and are cleared by Replay, Stop or an assembly reload; they are inspection data, not saved game settings. Capture extends beyond the minimum length through recovery, with a 20-second/1,200-frame cap and an estimated 256 MB limit. If a limit is reached, the Lab states that the captured range was capped. Timeline playback and scrubbing are silent; use live Replay with Sound (live) for audio review.

In tap-to-fire mode, pressing Shoot when a glowing enemy is available immediately draws the sword and dashes to finish it; pressing Launch is optional. With no eligible glowing enemy, Shoot remains a pistol shot. The currently aimed glowing enemy takes priority; otherwise a ready target is selected normally. The finisher consumes the tap without queuing a pistol shot, and further taps during the committed draw or cut do not cancel it.

The draw animation and dash start together. The approach does not restart when
the draw transitions into the swing. Shoot-triggered finishers do not activate
Launch speed boost or nitro ignition; manual Launch still provides its normal
boost. Dash wind and slow motion begin with the approach, while sword ribbon and
swish effects follow the swing. Draw and swing speeds remain tunable in this Lab.

Launch also immediately draws for a glowing enemy within the full finisher range (150 world units by default), then the dash covers the distance to that enemy. There is no requirement to ride within 14 metres first. Adjust the full range in TUNE > TRIGGERS > Sword Finisher Targeting > Full dash / target range. The nearby-target priority settings only choose which ready enemy to prefer when several are available. Healthy enemies remain ineligible. One successful sword kill ends Launch; the sword return, explosion, slow motion and traffic recovery continue normally without automatically finishing another enemy.

The dash continues directly toward the enemy and stops short along its approach line. Blade offsets do not steer the motorcycle into a side lane or move the enemy. Manual steering remains active during the dash and adds its own sideways movement to that approach. After contact the normal motor immediately owns movement again, even while the animation continues. Steering far enough away can miss the strike. Frontal targets prefer the left swing; only targets more than 0.5 world units to the right select the right swing (Bike Sword Combat > Pickup targeting > Right Attack Side Threshold).

Traffic recovery grace (s), in Dash & slow motion, controls the extra safe time after both the animation and its world slow motion finish. The default is 0.8 real seconds. Traffic collisions are ignored from the committed draw through that recovery window; ordinary Launch pursuit alone does not grant protection. Cars stay intact and remain solid for other actors. If grace expires inside a car, only the overlapping bike/car pairs stay ignored until separated, avoiding an immediate crash on expiry. Walls remain solid. Pause freezes grace. Save for game persists the grace setting alongside animation tuning; older drafts receive the default without resetting their existing sliders.

Validation covers full left/right takes, all transition phases, backward/forward state restoration, particles and trails, orbit/zoom on a frozen frame, restoring pre-death enemy visuals, unchanged live damage/score/movement during seeks, recording-clock cleanup, immediate world-slow-motion cancellation and a subsequent finisher with slow motion disabled. Tuning persistence and draft migration are checked on a prefab copy.

Particle snapshots use Unity's supported playback/particle/trail restore APIs: [ParticleSystem.GetPlaybackState](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.GetPlaybackState.html).
