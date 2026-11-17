# Gap Dash Lab

Open **CyberBiker â†’ Gap Dash Lab** outside Play mode and click **Start preview**.

The gap assist is automatic **only during Launch mode**. Ordinary riding keeps manual steering. The Lab toolbar has a **Launch** toggle to preview and compare both modes; its speed selector isolates alignment at a fixed speed. Its default capture radius is **5 m**, with **0.85 seconds of approach lookahead** and a **65 m maximum trigger distance**. It straightens the bike and moves it into the centre before the rear bumpers, then holds that line until the bike clears both cars. **All car collisions remain enabled; the full bike must fit, and the alignment path must be clear. Forward speed stays under the normal riding motor, with no boost or slow motion.** Wind streaks, a recorded whoosh and a lower, closer camera give the pass its emphasis.

Once a pass starts, steering is locked: holding, reversing or alternating left/right cannot cancel it or push the bike away from the line. Normal steering returns only after the complete bike clears both cars plus the exit clearance. Pause, death, sword finishers and genuine collisions still take priority.

Running out of Launch charge or cancelling Launch after commitment no longer
abandons the bike between the cars. The current pass completes with solid car
colliders and normal forward movement; a new pass still requires Launch. Replay
and Loop start each Lab take with a fresh Launch charge, independently of the
previous take's energy.

The earlier collision-forgiveness option has been removed. The assist does not disable colliders, ignore contact pairs or move cars. If the full bike cannot fit, or there is insufficient distance left to complete a wide correction before the bumpers, it does not fake a pass. The Lab status explains these rejected cases.

## Tuning

All controls on the left are editable before and during preview. They include:

- **Snap radius:** how close sideways you must be to the gap centre.
- **Predictive side entry:** recognizes sideways momentum toward a gap, including
  countersteering to brake that slide. It can commit before the bike reaches the
  base snap radius when the projected approach enters it. It never overrides the
  maximum forward catch distance. That distance is measured from the front of
  the bike to the nearest rear bumper. If there is too little room to align
  safely within that limit, the assist rejects the pass instead of starting
  early or passing through a car.
- **Approach time / maximum trigger distance:** how early the bike catches a nearby gap. The default starts early enough to complete the lateral movement before reaching the bumpers.
- **Side clearance / maximum gap width:** valid gap dimensions. Greater side clearance is stricter. The complete upright bike, including its handlebars and body colliders, must fit.
- **Alignment time / maximum alignment speed:** minimum blend duration and sideways speed limit. Wide corrections take longer; a leaning bike also gets time to straighten before entering the corridor.
- **Maximum dash time / exit clearance:** minimum safety timeout and the distance its rear must clear the cars. The timeout automatically extends when needed to finish a normal-speed pass.
- **Cooldown:** repeat timing between passes. Steering does not cancel a committed pass.
- **Camera:** blend in, return, position, angle, FOV, kick and shake.
- **Wind:** density, width, length, radius, lifetime, speed and colour. Particle speed affects the airflow, not the motorcycle.

## Preview

Choose **Open Gap**, **Tight Gap**, **Narrow Gap**, **Blocked Exit** or **Single Car**. The preview uses the real Car_04 prefab. Tight Gap leaves insufficient room for the handlebars; Narrow Gap is smaller still. Both should reject automatic entry. Blocked Exit puts a third car in the passage. Other controls set preview riding speed, sideways offset, car spacing, initial distance and test steering. Scenario changes apply on **Replay**; they do not save as gameplay settings.

**Easy passing** loads the 5 m / 0.85 s / 65 m preset while retaining your camera and wind tuning. It starts the preview cars 65 m ahead to demonstrate wide corrections with enough road to align. Existing Lab drafts receive the wider, earlier detection range on their first opening after this update. Save for game to keep further edits.

Use **Pause**, **Step**, **Replay** and **Loop** to inspect the actual physical passage. Camera and effect controls also update while paused. **Orbit inspection view** allows dragging to rotate and scrolling to zoom around the rider. Turn it off to inspect the actual gameplay camera.

The preview keeps its generated road tiles fixed for the session. Automatic loops rewind the bike and cars on that same stretch, with the camera following immediately; the gameplay road streamer is not changed.

The **Camera / VFX time** slider freezes the scene and previews the effect envelope at a chosen time. It does not replay the physical traffic simulation; use Replay for the complete approach, alignment and pass.

## Save

Click **Save for game** in the top toolbar to persist the tuning in `Assets/Resources/GapDashTuning.asset`. The game loads that profile automatically. **Load saved** restores the saved values; export/import JSON presets to compare alternatives. Saving tuning preserves the assigned wind material and audio recording. Preview edits alone do not change the saved scene or profile.

**Stop** exits preview and restores the previous Play start scene and audio volume.
