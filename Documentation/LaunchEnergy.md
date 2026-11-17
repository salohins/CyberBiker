Launch now uses a shared energy bar above the Launch button.

- A fresh run starts at 100% by default. A full bar gives 7 real seconds of
  Launch. The bar drains only while Launch is active and freezes during pause.
- Cancelling, aiming or reactivating Launch does not refill it. Crashing into
  cars or borders burns the entire remaining bar and stops Launch immediately.
  Exhaustion ends the boost. A committed sword attack can still finish safely.
- Below 10%, Launch cannot be activated. The bar says EARN SCORE until enough
  charge is available again. Keyboard Space uses the same restriction.
- Scoring moves refill immediately. Their awarded points, including the current
  multiplier and repetition reduction, contribute toward a full bar. The default
  is 1,500 points for 100% charge. Banking a combo does not refill it a second time.
  Ordinary distance score and direct score awards also refill the bar.
- Pistol hits, pistol kills and assisted gap passes still award score but do not
  refill Launch. The incidental close-shave rewards inside an assisted gap do
  not refill it either. Independent close shaves, dodges and sword finishers
  retain their energy rewards.
- Shoot-triggered finishers remain available with an empty bar, do not consume
  Launch charge, and can earn charge from the finisher reward.
- A new run resets charge to the configured starting percentage. Remaining charge
  is run state; the TUNE configuration is what persists across app launches.

Open TUNE > RIDE > LAUNCH & COMBAT:

| Setting | Default | Effect |
|---|---:|---|
| Full Launch bar duration (real seconds) | 7 | Full capacity expressed as usable boost time; shorter means faster drain. |
| Score to refill a full Launch bar | 1,500 | Lower values refill faster. |
| Minimum charge to start Launch (%) | 10 | Required charge for manual Launch activation. |
| Launch charge at run start (%) | 100 | Applied on the next new run or restart. |

Apply saves these controls on the device. PRESETS exports/imports them with the
rest of the balance settings. Applying a new configuration never refills or
empties the current bar. Older presets retain the new fields' defaults.

The HUD uses the existing Launch button's Canvas, placement and safe area. Its
cyan segmented bar has no touch hit area and uses no bloom/emissive material.

Native Unity checks cover drain, cancellation, repeated activation, pause,
exhaustion, activation threshold, real scoring actions, multiplier refill,
exact-once bank accounting, direct/distance score, clamping, TUNE draft/apply,
preset compatibility and persistence across a scene restart. Left/right finisher
checks additionally start with zero energy to verify independent Shoot finishers.
No new Android APK or physical-device test was performed for this change.
