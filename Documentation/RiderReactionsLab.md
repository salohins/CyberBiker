# Rider Reactions Lab

Open **CyberBiker → Rider Reactions Lab** in Unity, outside Play mode. Click **Start preview**. The lab starts an isolated Play preview of the saved Level with the actual player rig and reaction code. Stop the preview before using another Lab.

Choose a scenario at the top:

- **Idle Ride:** authored riding pose, breathing and idle sway.
- **Car Approach / Close Pass:** a real car approaches, passes beside the bike and triggers a body response. Set its side, starting distance, relative ride speed and pass strength.
- **Nearby Enemy:** a brief ambient glance at an enemy.
- **Visible Shooter:** an enemy firing at the player, which takes head-attention priority.
- **Shooter Leaves View:** the attacker leaves the gameplay camera view after two seconds, allowing inspection of the return to riding.

The car scenarios share the same approach/pass sequence, so you can inspect both anticipation and recovery in either. Scenario positions and inspection camera controls are preview tools, not saved gameplay tuning.

## Tune and inspect

All reaction controls stay editable before and during the preview. Edits apply immediately. Smaller smoothing values turn faster; larger values turn more gradually. Car head/neck controls do not add a neck layer to enemy-looking behavior. Body responses yield to aiming; enable **Aiming pose** to inspect that ownership boundary.

Use **Pause**, **Step**, **Replay**, and **Loop** to inspect timing. Drag **Reaction timeline** to pause and rebuild the reaction at a chosen point. Changing tuning while paused rebuilds that same moment. The scrubber evaluates the reaction over a captured riding/aiming pose; **Replay** runs the actual animated rider with live steering. When switching aiming pose, the lab briefly runs to settle the real animation before capturing its new base pose.

Drag the image to orbit, Shift-drag to pan, and scroll to zoom. **Head & neck** frames the helmet for close inspection; **Full bike** restores wider framing. **Game view** uses the actual gameplay camera for checking attacker visibility. Orbiting the inspection camera does not change what counts as in view for the rider’s attention.

## Keep changes

**Save for game** is always in the top toolbar. It saves only the Rider Life component’s reaction tuning on `Assets/Art/Models/Player_Last.prefab`, for the game and later builds. It does not save scenario positions, preview camera orbit or unrelated player settings. **Load saved** restores those saved values. Export/import JSON presets to compare alternatives.

The draft survives closing the window. Preview edits alone do not change the saved player prefab or scene. **Stop** exits the preview and restores the previous Play start scene and audio volume.
