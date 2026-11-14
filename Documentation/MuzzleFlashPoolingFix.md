# Muzzle-flash hierarchy cleanup

MuzzleFlash.OnDisable previously called PrefabPool.Release immediately. When an aim/weapon ancestor was being deactivated, Release reparented the child during Unity's hierarchy transition and logged the exact Toon Muzzleflash 8 / MuzzleFlashPosition error shown in the Console.

OnDisable now schedules a return through a scene-owned queue outside the weapon hierarchy. The queue runs at the end of LateUpdate, sleeps when idle and checks the pooled generation before returning anything. It uses reused request buffers and is destroyed with its scene. Normal three-frame muzzle-flash expiry still uses the original immediate return path.

MuzzleFlashDisableChecks.Run reproduced the original error in the validation copy, then passed ancestor/component disable, immediate off/on, newer-lease protection, three-frame expiry, reuse, 40 rapid off/on cycles and scene unload with a pending return after the fix. No missing-script components were found on the loaded Level objects; the existing asset/Visual Scripting warnings are separate from this runtime parenting error.
