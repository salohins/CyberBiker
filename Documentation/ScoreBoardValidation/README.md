# Compact score board motion

Validated with Unity 6000.6.0f1 in the isolated RefactorValidation project.
The user's open Unity Editor and saved scene were left running.

`BoardMotion.mp4` and `BoardMotion.gif` are a deterministic native Unity UI
capture at 960 × 320, exported at 30 fps (GIF at 20 fps). They show repeated
pistol hits, driving rewards, multiplier gains, a finisher, dodges and banking.
The actions are scripted for the capture; the actual runtime board, arithmetic,
event handlers and animation code render the result. The background is isolated
to make the movement readable. The preview is silent and contains no Carbon
recordings. Multiplier.png and Banking.png are frames from that sequence.

Result.txt also records the full native integration run: real projectile hits,
enemy deaths, close-passing traffic, banking into the run score, original-pitch
Carbon cue dispatch, pause, input pass-through and reduced motion. The new board
checks verify a bounded score spring, multiplier sweep/echo, exact bank count-up
and preservation of the 560 × 133 plate and 108 × 83 multiplier base dimensions.
All checks passed. Existing missing-script/Visual Scripting type warnings remain
in the project; no new scoring exception or compiler error was reported.

The board caches its effect graphics at initialization. Awards reuse the score
and badge springs, sweep, four streaks, two outline echoes and twelve shards;
they instantiate no presentation objects. This run verifies behavior and native
rendering, and is not a mobile performance benchmark or subjective audio review.
