# World-space score numbers validation

Validated in an isolated Unity 6000.6.0f1 project copy using the real level, camera and bloom profile.

- Native integration checks passed (Result.txt): scoring arithmetic, actual projectile hits/kills, dodges, traffic close passes, pause, banking and reduced motion.
- Eight reusable world-space canvases contain numbers only, ignore input, follow enemy translation and show camera parallax. Depth occlusion remains active.
- Fixed the dark backing: its custom Graphic previously lacked CanvasRenderer. The regression now checks a real submitted four-vertex/six-index mesh, 22 pixels of total horizontal padding, 64-pixel height and 0.96 opacity. WorldAnchor.png and NoBacking.png show the same moment with and without the backing.
- Appearance brightness starts at 0.94 and settles over 0.22 real seconds to 0.82. Native checks confirm that it decreases to the configured settled value. WorldAnchor.png and Settled.png show the two phases. Reduced motion suppresses the initial brightness pulse.
- Close-pass numbers appear horizontally between the closest car/bike collision surfaces and above their bounds for readability. CarPass.png captures the actual close-pass award; the placement assertion uses live car/bike bounds.
- Near-camera scaling, pooling, head-on occlusion and the smooth rise/fade remain checked. Global bloom settings are unchanged.

Existing missing-script and Visual Scripting warnings are outside this change. This verifies Editor integration/rendering, not Android performance.
