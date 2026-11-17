# Gyroscope / tilt steering

In the in-game **TUNE → CONTROLS** section, choose **SWIPE** or **GYROSCOPE**, then **APPLY**. The choice and tuning save on the device and survive restart. Swipe remains the authored default; older saved Tune settings retain their values. Reset returns to the original control mode and tuning.

Hold the phone facing you in your normal riding position while applying gyro mode. The first valid gravity sample becomes neutral. To recenter without switching mode, tap **CALIBRATE ON APPLY**, hold comfortably, then **APPLY**. Calibration is queued so Cancel discards it; the neutral pose is intentionally not saved across application restarts. Screen rotation and returning from background/focus loss also recenter safely.

- **Tilt sensitivity:** 1× reaches full steering at 20° from neutral; higher values require less tilt.
- **Tilt dead zone:** ignores small movement around neutral (default 2°).
- **Tilt smoothing:** response time in real seconds (default 0.12; zero is immediate).
- **Invert tilt:** reverses left/right.

Tilt changes steering only. FIRE, screen taps, aiming and LAUNCH remain touch controls. Swipe gestures do not steer in gyro mode. Old full-screen steering touch areas are disabled while tilt is active, while action-button touch targets remain available. Desktop keyboard steering remains available for testing.

Gyro mode uses the gyroscope's gravity vector to preserve steering while a tilt is held; it does not treat rotation rate as a steering angle. Devices without a gyroscope use accelerometer tilt. Without either sensor, swipe remains active and the menu reports the fallback. A phone held flat with no usable screen-plane gravity projection returns neutral instead of unstable steering.

Unity's screen-orientation compensation is respected; manual portrait/landscape mapping is applied only when compensation is disabled. See [Unity Input.compensateSensors](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/input/compensatesensors). Smoothing uses unscaled time, so combat slow motion does not add input latency. Pause suppresses steering output; sensors remain available for queued calibration. Sensor enable state and update interval are restored when switching away, losing focus or disabling the input component.

## Verification

`GyroscopeChecks.Run` exercises deterministic sensor samples through the real player input, both landscape orientations, held tilt, neutral/dead zone, inversion, frame-rate-independent smoothing, pause/focus, firing/drag separation, unsupported-sensor fallback, actual Tune controls, Apply/Reset and persistence across scene restart. `TestingSettingsChecks.Run` and `TapPistolChecks.Run` cover the pre-existing tuning and shooting behavior. Native Editor captures and results are in `GyroscopeValidation`. Physical sensor feel still needs a phone playtest.
