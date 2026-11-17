Independent finisher dash camera

Finisher Lab has a dedicated Dash camera group, open by default: enabled, FOV
offset, camera-local X/Y/Z position, pitch/yaw/roll, optional attack-side mirror,
target framing, blend in and blend out. Transition durations use real seconds.
Existing settings are grouped under Swing & impact camera and do not drive the
dash layer. Gameplay applies the new layer over the normal camera and crossfades
into the existing swing/impact view. Paused edits remove and reapply only the
camera contribution, without accumulation or advancing gameplay.

The Lab now previews the actual Shoot-triggered finisher, without the manual
Launch/nitro camera. Drafts and presets migrate missing dash controls from their
existing swing offsets; version 1 and version 2 JSON import are supported.

Result.txt: native checks for all twelve controls, vectors, actual import/export,
old presets and future-version rejection, isolated prefab persistence, left/right
live and paused views, exact FOV edits, mirrored/fixed position and rotation,
independence from swing/impact sliders, repeated refresh without drift or gameplay
advancement, transition timing, replay and the actual strike/kill/explosion.

RecordedTake.txt: full left/right take capture with slow motion on/off, retained
animation/particle transitions, backward/forward seeking and stepping, orbit/zoom,
and unchanged live score/damage/position while scrubbing. The recorded Drawing
camera restores its exact FOV, world position and rotation. The recording includes
the independent dash camera while LaunchCount remains zero.

Images and Preset.json use deliberately altered test settings, not recommended
or shipped defaults. DashGameCamera.png is from the direct-finisher recorded take.
No Android build or physical-device playtest was performed for this editor task.

Combat camera preview follow-up: the Lab pins the saved Combat Riding camera
before starting a live finisher or capturing the first take frame. Dash, swing
and impact remain additive to this view. The override is released with the Lab
view; ordinary gameplay and player-build camera mode selection are unchanged.
CombatBaseLive.txt records the repeated native checks, including saved base FOV
and pitch through both attack sides and after the target dies. CombatBaseTake.txt
records full take checks with additional assertions for the first recorded
frame's combat pitch/FOV and combat mode remaining active after the kill.
The saved CameraLabRig profile and level scene match the validation copy.
