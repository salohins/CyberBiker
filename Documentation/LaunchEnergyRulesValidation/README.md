Launch reward and crash rules

Car/border collision kicks and the Hit/GameOver states empty the entire Launch
bar and immediately disable its boost. This is independent of the score/combo
banking rules. Cancellation and aiming still preserve remaining charge.

PistolHit, PistolKill and GapPass keep their normal score/combo rewards but give
no energy. NearMiss awards while gap assist is active also give no energy, so the
two cars cannot refill the bar indirectly. Banking these points gives no energy.
Independent close shaves, dodges, clean driving and sword finishers still refill.

EnergyRules.txt covers reward exclusions, incidental gap close shaves, banking,
remaining eligible rewards, multiplier scaling and a crash while Launch is active,
plus the existing drain, pause, persistence and preset tests.

The car collision regression fills the bar before driving the real player into
a solid car and asserts zero charge on the actual crash. The gap continuity
regression exhausts the bar during an assisted pass and checks that completion
does not refill it via either GapPass or incidental near-miss awards.
