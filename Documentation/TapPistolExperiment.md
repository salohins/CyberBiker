# Tap-to-fire pistol experiment

Tap-to-fire is enabled by default in GameplayManager. On PC, left-click the gameplay area, press F, or right-click once. On mobile, use the existing aim control, now captioned FIRE, or make a short tap in the central gameplay area. Holding does not repeat.

The first tap draws the pistol and fires when the original animation/aim rig is ready. Each tap requests one shot with no fire-rate cooldown. Taps received during the initial draw are retained. The pistol stays ready for 1.2 real seconds after the last tap/shot, then starts the existing holster transition. Further taps within that window do not redraw. Enemy tracking, aim camera, recoil, hit markers, sounds and impact reactions remain in use.

Pause, death, collision state and Launch cancel pending taps. A new tap can return from Launch through the existing sword-to-pistol transition. Steering drags do not fire. Real UI controls exclude general click/touch polling, preventing duplicate button shots. Desktop steering zones pass mouse clicks through in tap mode; A/D still steers. Mobile steering controls retain their original UI behavior.

GameplayManager settings:
- Pistol Interaction: Tap To Fire or Aim And Auto Fire.
- Pistol Holster Delay: idle time before holstering.
- Tap Pistol Can Kill: enabled for this experiment. Player pistol contacts may kill and use the existing explosion/death path. Disable it to require sword finishers while retaining tap controls.
- Pistol Damage Per Shot: unchanged.
- Seconds Between Pistol Shots: applies to the original automatic mode; tap mode bypasses it.

The FIRE button contracts when a tap is accepted, then pops and tints its caption when a shot fires. A red world-space laser and hit dot show the pistol's aim while ready. The laser shares the bullet aim calculation, ignores the rider/projectiles, stops on nearer obstacles/enemies, and hides on holster, pause or Launch. Color, width, dot size and range are in the player prefab's Pistol Aim Laser component. Its shared material is in Resources for builds.

TapPistolChecks.Run passed actual player/button/projectile checks: first-tap draw/single shot, eight rapid taps producing eight shots without cooldown, no automatic repeat, no redraw between taps, idle holster/redraw, pause/resume, screen tap vs drag, UI exclusion, Launch cancellation/return, fallback automatic fire, lethal pistol contact and laser origin/obstacle clipping. ShootingFeedbackChecks.Run explicitly selects Aim And Auto Fire and passed the existing shooting/feedback regression. Reports/captures are in TapPistolValidation. Checks ran in the Unity Editor; no Android build was made.
