# Helmet warning detection fix

The green spider-sense strokes were still installed and enabled. The warning tested only solid player collision bounds. Those bounds sit around the motorcycle, while ordinary enemy shots aim at player position + 3 metres. A real enemy shot passed above the padded solid bounds, so the helmet warning never activated even though its green trajectory was visible.

Warning detection now also tests a small bounds envelope around the animated head and upper torso. The existing Helmet Sense Padding expands this envelope as well as the bike bounds. This creates no collider and changes neither bullet damage nor physical collisions. The existing twelve green strokes, head attachment, brightness, approach-dependent pulse and smooth fade remain intact.

Tune **GameplayManager → Projectile Combat → Helmet Danger Sense**. The effect still appears only for an incoming in-flight path through the rider/bike warning region, fades when steering clear, ignores evaded shots and hides during pause.

Regression: EnemyTrajectoryChecks now includes an unmodified real enemy gun shot, in addition to the existing moving-frame trajectory, warning, pause, re-entry, evasion, pooling and near/far pulse checks. The full native suite passed; evidence is in HelmetSenseValidation, including NaturalShotHelmet.png from the ordinary enemy shot.
