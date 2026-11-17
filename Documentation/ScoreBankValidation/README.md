# Bank score on damage and collision

Damage, collision kicks, entering Hit state and game over bank half the current
base points multiplied by the earned multiplier (rounded down to whole points),
then clear the chain and return the multiplier to one. Already banked score
stays untouched. The same impact can notify damage, state and physics systems;
only the first notification can credit a nonempty chain, so the award is never
halved again. The usual quiet-time combo expiry still banks the full amount.

The dedicated RunSession.AddBankedCombo credit accepts already-earned points
after the session has entered Hit or GameOver. This avoids silently rejecting
the bank during a state-change callback. Generic AddScore still rejects paused
and invalid credits. ScoreChanged updates the existing HUD and final score.

Native Unity checks in Result.txt passed for actual player damage with a
multiplier, collision Hit state, repeated impact callbacks, and game-over
banking. The full existing projectile, enemy death, traffic, score board,
pause and reduced-motion checks also passed. Existing project warnings are
unchanged. Android builds and their package checks are recorded separately
alongside the APK under Builds/Android.
