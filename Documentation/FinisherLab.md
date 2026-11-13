Open CyberBiker > Finisher Lab. Stop and restart an existing Lab preview once after a code update. Your draft sliders are kept.

The fixed toolbar at the top contains Save for game, Reload saved defaults, Export preset and Import preset.

- Replay tests the current draft on a fresh target. Left/Right chooses the enemy side.
- Save for game writes only the Lab's animation, finisher camera, particle and sound tuning fields to Player_Last.prefab. Future runs and builds use those values.
- Export preset writes a separate JSON settings file. Import preset loads one into the draft. Neither button exports a recorded take.
- Reload saved defaults discards draft edits and reads the player's currently saved tuning.

World slow motion is a master switch for the dash, hit and explosion world-time effects. Turning it off immediately cancels any active finisher slow motion. It does not change animation playback speed, the impact-pose hold, or the dash duration. For example, Swing speed 0.1× still makes the sword animation ten times slower. Animation 1× resets draw, swing and sheathe playback speeds to authored speed while keeping transition settings.

To inspect the complete finisher, press Record full 3D take. The Lab starts a fresh sequence and captures the actual blended poses, particle playback state and trails, sword ribbon geometry, materials, lights, impact flash and game camera. Capture uses fixed steps for the finisher's real-time clock as well as Unity's scaled clock; ordinary gameplay and player builds keep their normal clocks. Recording is silent so audio DSP timing cannot drift against offline capture.

When recording finishes, the live run pauses. Use Whole finisher (s) to seek backward or forward, or use the frame buttons. Jump buttons select Drawing, Striking, Sheathing, Recovering, Hit or Explosion. Playback speed changes only the viewing speed of the recorded take. The recorded world-speed display follows the selected frame.

Drag the recorded image to orbit. Middle-drag pans; the wheel zooms. Front/Left/Right/Back choose fixed views; Game camera returns to the recorded cinematic view. Orbiting never changes gameplay camera settings or advances the particles. These are 3D snapshots, so the enemy and effects can be inspected at earlier times even after their original objects have been destroyed.

Changing tuning marks a take as stale. Record again to include the edits. Takes are temporary and are cleared by Replay, Stop or an assembly reload; they are inspection data, not saved game settings. Capture extends beyond the minimum length through recovery, with a 20-second/1,200-frame cap and an estimated 256 MB limit. If a limit is reached, the Lab states that the captured range was capped. Timeline playback and scrubbing are silent; use live Replay with Sound (live) for audio review.

The dash continues directly toward the enemy and stops short along its approach line. Blade offsets do not steer the motorcycle into a side lane or move the enemy. Manual steering remains active during the dash and adds its own sideways movement to that approach. After contact the normal motor immediately owns movement again, even while the animation continues. Steering far enough away can miss the strike. Frontal targets prefer the left swing; only targets more than 0.5 world units to the right select the right swing (Bike Sword Combat > Pickup targeting > Right Attack Side Threshold).

Traffic recovery grace (s), in Dash & slow motion, controls the extra safe time after both the animation and its world slow motion finish. The default is 0.8 real seconds. Traffic collisions are ignored from the committed draw through that recovery window; ordinary Launch pursuit alone does not grant protection. Cars stay intact and remain solid for other actors. If grace expires inside a car, only the overlapping bike/car pairs stay ignored until separated, avoiding an immediate crash on expiry. Walls remain solid. Pause freezes grace. Save for game persists the grace setting alongside animation tuning; older drafts receive the default without resetting their existing sliders.

Validation covers full left/right takes, all transition phases, backward/forward state restoration, particles and trails, orbit/zoom on a frozen frame, restoring pre-death enemy visuals, unchanged live damage/score/movement during seeks, recording-clock cleanup, immediate world-slow-motion cancellation and a subsequent finisher with slow motion disabled. Tuning persistence and draft migration are checked on a prefab copy.

Particle snapshots use Unity's supported playback/particle/trail restore APIs: [ParticleSystem.GetPlaybackState](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.GetPlaybackState.html).
