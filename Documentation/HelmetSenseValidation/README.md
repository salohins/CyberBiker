# Restored helmet spider-sense validation

Native Unity 6000.6.0f1 integration checks passed in an isolated project copy. See Result.txt and NaturalShotHelmet.png.

The new regression initially failed on an ordinary enemy shot before the fix. The shot aims at world height 3 above the player root; the solid bike collision bounds and their warning padding sat below its path. Geometry.txt records those live bounds and the head pose.

After adding a warning-only envelope around the animated head and upper torso, the same actual enemy shot triggers visible green strokes. The full existing trajectory suite also passed: exact moving-frame bullet/line alignment at driving speed and in slow motion, no pre-fire halo, smooth fade when steering clear, re-entry, pause freeze/hide, evasion cleanup, pooled reuse, shooter destruction and increasing pulse frequency as a real bullet approaches.

The warning-only bounds create no collider. Projectile collision, damage and the existing helmet material/brightness are unchanged. Screenshots were visually reviewed. Android was not built for this change.
