The Android sword failure was reproduced in a compiled standalone player, independently of touch input and GPU skinning. Sampling the original Generic clips on the transform-only sword skeleton differed from the same samples in the Editor: 3,658 bone mismatches across 56 frames and 106 transforms, with a maximum rotation error of 177.6378 degrees. Reconstructing a fresh Generic clip gave the same failure. Copies containing raw Legacy transform curves produced zero mismatches.

BikeSwordCombat now resolves its seven clips through Resources/SwordRuntimeClips before creating its sampling skeleton. These runtime copies preserve every transform curve, duration, sample rate and loop setting. The original Generic clips, preview Avatar/controller, Humanoid riding/pistol Animator, RigBuilder, animation blending and combat timing remain intact.

SwordRuntimeClipBuild generates the binary library and checks its source fingerprint before every player build. The sword import command also refreshes it. The library can be regenerated with CyberBiker > Animations > Rebuild runtime sword curves. It contains only generated clip subassets; regeneration does not rewrite the preview clips or gameplay scenes.

Validation:

- SwordRuntimeClipChecks: seven source/runtime pairs, 56 sample times and 5,824 animated bone poses match, including endpoints and unordered sampling.
- Compiled player probe using the actual runtime library: 56 frames, 106 transforms, zero position, rotation or scale differences from the Editor. Every sword clip passes.
- SwordCombatChecks: left/right cuts, draw/sheath cycles, mounted sword between enemies, impact/slow motion, obstruction, pause/resume, trails and fast sword-to-pistol handoff pass.

The standalone probe sources are stored in tools/validation/SwordRuntime. To rerun in an isolated project copy, copy RuntimeSwordProbe.cs under Assets and SwordPlayerBuildProbe.cs under Assets/Editor, run SwordPlayerBuildProbe.Run in a Windows-targeted batch Editor, then launch Builds/SwordProbe/SwordProbe.exe with -batchmode -nographics -probeOutput followed by an absolute report filename. The probe scene is separate from the game's build scenes.

The replacement Android test APK uses version code 2 and the same package/signing identity as the first APK. Physical phone playback still requires confirmation; no USB debugging device was attached during validation.
