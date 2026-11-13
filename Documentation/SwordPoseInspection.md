Open CyberBiker > Sword Clip Inspector to inspect the authored animations independently of a finisher. It works in Edit mode and while the game is paused. Select Left swing, Right swing, Draw or Sheathe. Scrub Clip position, use the two frame buttons, or play at 0.1–2× speed. Drag the image to orbit, scroll to zoom, and use the Front/Left/Right/Back camera presets. The inspector has no combat particles, camera shake, slow motion or pose blending.

The comparison checkbox reproduces the old torso correction for diagnosis only; it never changes gameplay or the animation assets. Leave it unchecked to inspect the authored pose.

The production finisher previously rotated DEF-spine.001 so that a vector from the torso to the sword tip faced along the dash. At the authored contact frame this added approximately 79 degrees on the left and 67 degrees on the right. The correction persisted into the cached transition pose, and the left turn reached approximately 92 degrees at the end of the swing. This changed the whole upper body and the head, then blended that distorted pose into sheathing.

Production no longer applies that correction to either the rider or the sword trail sampling skeleton. The short dash follows the direct approach toward the enemy and stops short on that same line. Authored blade offsets never move the bike into a side lane. The enemy keeps its position and lane. Source clips, playback rates and transition settings have not been retimed by the pose fix.

To test the updated finisher, stop the old preview and start a new run. Finisher Lab contains a button opening the clip inspector, as well as the live draw/swing/sheathe speed and transition controls. Save for game persists Lab tuning; moving the clip inspector's timeline or camera does not change game settings.

Validation: native renders of both raw and old-adjusted contact poses, sheathe start/middle/end renders, and the complete SwordCombatChecks suite. Both one-shot attack sides, draw/sheath/ride cycles, obstruction, pause, effects, slow-motion recovery and cancellation into the pistol pass.
