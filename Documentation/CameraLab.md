# Camera Lab

Open **CyberBiker → Camera Lab** in Unity. Stop any existing Play session first, then click **Start preview**.

Choose **Riding**, **Combat Riding**, **Auto Riding**, **Cruising Aim**, **Combat Aim**, **Auto Aim**, their left/right shoulder previews, **Airborne**, **Launch**, **Finisher**, or **Dodge** in the top toolbar. Regular views switch immediately. Switching to or from a finisher/dodge starts a fresh preview of that action, so allow a moment for Play mode to restart.

## Cruising and combat riding

**Riding** previews cruising with no enemies. **Combat Riding** previews the separate composition used when enemies are nearby and the pistol is holstered. Each has its own offset, FOV, pitch, yaw and roll. The **Combat riding** foldout also has independent steering X/height/forward shifts, steering camera lean and follow response.

The combat camera starts with a copy of your existing riding settings, including settings from older saved Camera Lab profiles and drafts. Change the combat values to create the difference you want, then press **Save for game**.

The automatic switch for both riding and aiming is controlled in **Combat switching**:

- **Enable combat views:** enables the automatic combat riding and aiming compositions.
- **Enemy range (m):** entry radius around the player root, measured in the horizontal road plane. Default: 60 m. Living enemies ahead, behind or beside the player count. Inactive/dead enemies and enemies queued for later encounters do not count.
- **Exit range padding (m):** extra distance an enemy can move away before the camera starts returning. Default: 10 m. This prevents repeated switching at the boundary.
- **Return delay (s):** how long enemies must remain absent/outside the exit radius before returning to cruising. Default: 1 second of gameplay time; pause freezes it.
- **Blend time (s):** duration of the eased combat weight transition. Default: 0.8 seconds. The regular camera's position/FOV follow smoothing still applies.

Choose **Auto Riding**, run the preview and toggle **Nearby enemy** or change **Enemy distance (m)** to test the actual automatic transitions. The status shows the combat blend percentage. Use Riding and Combat Riding to position each view; mouse positioning is disabled in automatic views to keep the two compositions unambiguous.

Aiming uses its cruising/combat compositions, and airborne retains its dedicated composition. Launch, finisher, close-dodge, speed vibration, passing and impact effects continue to layer over the camera.

## Cruising and combat aiming

Choose **Cruising Aim** to position the camera while aiming without enemies, or **Combat Aim** to position it while fighting. Each view has separate position/shoulder offset, FOV, pitch, yaw, roll, steering X/height/forward shifts, steering camera lean and follow response. Drag, right-drag, scroll and Shift+scroll edit the selected composition only. Each also has a **Left** and **Right** shoulder preview.

**Auto Aim** keeps the pistol drawn while you toggle **Nearby enemy** or adjust **Enemy distance (m)**. It blends between the two aiming setups using the same **Combat switching** range, exit padding, return delay and blend time as riding. This state stays consistent when drawing or holstering the pistol.

Existing saved aiming values remain the cruising aiming setup, and the combat aiming setup initially copies them. Existing combat riding settings are retained. Change the two aiming views to suit your game, then press **Save for game**. Old presets and drafts are upgraded automatically.

Automatic shoulder selection/glide, target/pistol framing and rider visibility remain shared support settings under **Shared aim shoulders & target** and **Shared pistol & visibility**. The shoulder distance itself is independent in each aiming view's offset X.

## Move the camera

- Drag the live image with the left mouse button to move the camera sideways/up/down.
- Right-drag to change pitch/yaw.
- Scroll to change distance; hold Shift and scroll to change FOV.
- Alternatively, edit the offset, rotation and FOV fields in the view's foldout.
- **Scene handles** adds position/rotation handles at the live camera in Scene view. Pause the preview for precise placement. **Frame rider** centres Scene view on the rider; **Use Scene view** transfers that composition to the selected view.

**Steering**, **Road position** and **Enemy side** let you check camera shifts, lean and automatic shoulders. Forced left/right aim views use their selected cruising/combat shoulder-distance setting and leave the automatic shoulder configuration intact. The speed field controls the base preview speed in metres per second. Launch adds the game's real boost.

## Inspect camera effects

The left panel includes steering shifts/roll, shoulder switching, target/pistol framing, rider visibility, airborne framing, speed vibration, passing/collision impulses, launch/nitro, sword finisher and close-dodge cameras.

Use **Test passing shake** and **Test collision impact** while the preview is running. **Replay** repeats a cinematic; **Hold impact** pauses at the sword contact or the peak of a close-dodge camera move. **Pause**, **Resume** and **Step** also work. Parameters remain editable while paused. Sound is optional.

Launch, finisher and dodge have their own position/rotation offsets. Moving these views leaves the regular riding offset alone. Finisher camera controls apply through the action envelope, so inspect the full action with Hold impact off before saving. For animation, slow-motion timing or pass-radius changes, use the separate Finisher Lab or Dodge Lab.

## Keep your changes

**Save for game** is always visible in the second toolbar. It saves all camera groups:

- Base camera, steering, aiming, visibility and shake settings go to `Assets/Resources/CameraLabRig.asset`. The gameplay camera loads this profile at startup. No profile is created until you save.
- Launch and finisher camera settings go to the existing player prefab, keeping the Finisher Lab in sync.
- Close-dodge camera settings go to the existing CloseDodgeTuning profile, keeping Dodge Lab in sync.

**Load saved** restores the saved game settings. **Scene defaults** loads the base camera from the saved Level scene, plus the saved launch/finisher/dodge camera settings. Neither button saves automatically. Export/import JSON presets to keep alternate camera setups; imported presets become a draft until saved.

Drafts survive closing the window or restarting Unity on this computer. Preview changes are temporary until **Save for game** is pressed. **Stop** restores the previous Play-mode start scene and audio volume; it does not save preview objects or change your open scenes.
