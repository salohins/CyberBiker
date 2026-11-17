# Forward gaze and neutral pistol aim

Camera pitch controls framing. With no enemy selected, the pistol and its laser now follow the auto-aim system's smooth return to road-forward instead of a ray cast from the camera. The fallback ray starts at the muzzle, so nearby cars/walls can still block a shot. Locked enemies retain their predicted aim point.

The rider also receives a small vertical head correction while holding the pistol without a target. This keeps the helmet's gaze level in the aiming pose. Ordinary riding retains the original authored head angle. The correction blends out when a target is acquired and is excluded during sword draw, strike, sheathe and recovery poses.

Optional tuning is on the player's **Bike Auto Aim → Neutral forward look**: enable/disable, response and pitch correction limit. Camera Lab compositions are unchanged.

Native Unity validation in the isolated project measured the original neutral helmet gaze at about 20 degrees downward. With the camera tilted down 30 degrees, the original fallback shot also pointed into the road. The focused check now verifies level head/barrel/shot/laser, cover blocking, moving enemy acquisition, target loss, preservation of the authored head angle during normal riding and steering. Reports and captures are in `Documentation/NeutralAimValidation`.
