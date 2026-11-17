Launch gap continuity

Reproduced the interruption with the actual player and Car_04 colliders in the
Gap Dash Lab. Before.txt records the failure: an active, valid 2.11m gap pass was
cancelled when Launch energy exhausted, with zero completed passes. The same
Launch-state requirement was applied both to acquiring and continuing a gap.

The fix lets an already active gap continue when Launch ends. New acquisition
still requires Launch. Combat, impact recovery, disabled settings, disappearing
traffic, closed gaps, blocked paths and the safety timeout still take priority.
There is no collision ignoring, car resizing, extra speed or teleportation.

Lab Replay now resets only its preview Launch charge so repeated takes remain
comparable. Runtime cancellation still preserves the player's remaining charge.

Native checks exercise energy exhaustion during alignment, cancellation while
inside the car corridor, held steering, real solid colliders, no penetration or
damage, normal forward speed, restored Lab charge and no ordinary-riding capture.
The saved gap profile and the user's TUNE values are not retuned by this fix.
