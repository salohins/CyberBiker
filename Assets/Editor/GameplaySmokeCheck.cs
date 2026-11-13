using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Audio;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameplaySmokeCheck {
    private sealed class FixtureInput : CyberBiker.Player.IPlayerInput {
        private readonly TouchInputManager owner;
        public FixtureInput(TouchInputManager input){owner=input;}
        public Vector2 Steering=>new Vector2(owner.leftPressed ? -1 : owner.rightPressed ? 1 : 0,0);
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>owner.IsAiming;
    }
    private const string Pending = "CyberBiker.SmokeCheck";
    private static double started;
    private static int stage;
    private static float scoreBeforePause;
    private static float startZ;
    private static float startX;
    private static Car target;
    private static double steeringStarted;
    private static bool steeringChecked;
    private static Button aimButton;
    private static MuzzleFlash muzzle;
    private static int muzzleBefore;
    private static double aimPressedClock,firstPistolShotSeconds=-1;
    private static bool fixtureInputBound,tireResponseSeen;
    private static GameObject pickupProbe;
    private static ProjectileWeapon tuningWeapon;
    private static int audioBeforeShot;
    private static float engineElapsedBeforePause;
    private static Car nearWhooshProbe, farWhooshProbe;
    private static bool whooshSelectionChecked;
    private static int whooshCountBeforePause;
    private static bool pauseCrossingPlaced;
    private static CameraController passCamera;
    private static float shakeRemainingBeforePause, whooshRadiusBeforeDecay;
    private static float straightWindRush, pausedWindRush, pausedWindPan;
    private static float pausedJacketVolume, pausedJacketPitch;
    private static float pausedTreadPhase;
    private static float straightTireRate;
    private static int pausedTireEmissions;
    static GameplaySmokeCheck() {
        if (SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Attach;
    }
    public static void Run() {
        var inputSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
        var axes=inputSettings.FindProperty("m_Axes");
        for(int i=0;i<axes.arraySize;i++) {
            var axis=axes.GetArrayElementAtIndex(i);
            if(axis.FindPropertyRelative("m_Name").stringValue!="Submit")continue;
            Require(axis.FindPropertyRelative("positiveButton").stringValue!="space" && axis.FindPropertyRelative("altPositiveButton").stringValue!="space","Space must not activate selected UI buttons");
        }
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Attach() {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }
    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Tick() {
        if (!EditorApplication.isPlaying) return;
        try {
            var elapsed = EditorApplication.timeSinceStartup - started;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var manager = UnityEngine.Object.FindFirstObjectByType<GameplayManager>();
            if(!fixtureInputBound && player!=null && player.Health!=null && Time.timeSinceLevelLoad>.2f) {
                var touch=player.GetComponent<TouchInputManager>();touch.enabled=false;touch.SetAim(false);
                typeof(PlayerController).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,new FixtureInput(touch));
                fixtureInputBound=true;
            }
            if (elapsed < 2) return;
            Require(player != null && manager != null && (stage >= 5 || player.IsAlive), "Player and session must initialize");
            if (stage == 0) {
                Require(manager.gameState == GameState.Playing, "Level must start playing");
                Require(new SerializedObject(player.GetComponent<TouchInputManager>()).FindProperty("aimKey").intValue==(int)KeyCode.F,"PC aim must be bound to F");
                Require(new SerializedObject(player.GetComponent<CyberBiker.Player.BikeSwordCombat>()).FindProperty("launchKey").intValue==(int)KeyCode.Space,"PC launch must be bound to Space");
                // This suite exercises the existing free-aim fallback and weapon
                // tuning. AutoAimChecks covers enemy locking with this enabled.
                var targeting=player.GetComponent<CyberBiker.Player.BikeAutoAim>();
                if(targeting!=null) {
                    var targetingSettings=new SerializedObject(targeting);
                    targetingSettings.FindProperty("autoAimEnabled").boolValue=false;
                    targetingSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                var tread = player.GetComponent<CyberBiker.Player.TireTreadMotion>();
                Require(tread != null && tread.TreadPhase > 0, "Riding speed must drive the tire tread material");
                var tireParticles=player.GetComponent<CyberBiker.Player.TireContactParticles>();
                Require(tireParticles!=null && tireParticles.enabled && tireParticles.GroundedWheelCount>0,
                    "Rolling tires must detect road contact");
                Require(tireParticles.CurrentEmissionRate==0 && tireParticles.EmittedCount==0,
                    "Upright riding must not emit tire smoke or sparks");
                straightTireRate=tireParticles.CurrentEmissionRate;
                CheckTireParticleContact(player,tireParticles);
                var originalPistolSettings = new SerializedObject(manager);
                Require(originalPistolSettings.FindProperty("pistolDamagePerShot").floatValue == 15, "The global player pistol must deal 15 damage per shot");
                Require(Mathf.Approximately(originalPistolSettings.FindProperty("secondsBetweenPistolShots").floatValue, 0.45f), "The global player pistol must wait 0.45 seconds between shots");
                Require(GameAudio.Active != null && GameAudio.Active.ConfiguredClipCount >= 16, "Audio rig must load all downloaded clips");
                var audioPreset = new SerializedObject(GameAudio.Active);
                var engineAudio = GameAudio.Active.transform.Find("Bike engine cruise").GetComponent<AudioSource>();
                var launchAudio = GameAudio.Active.transform.Find("Bike engine launch").GetComponent<AudioSource>();
                var windAudio = GameAudio.Active.transform.Find("Speed wind").GetComponent<AudioSource>();
                Require(engineAudio.clip != null && launchAudio.clip != null && engineAudio.loop && !launchAudio.loop && GameAudio.Active.EngineLaunchCount == 1,
                    "Engine must start its launch recording once and loop its cruise recording");
                Require(Mathf.Approximately(engineAudio.spatialBlend, 1) && Mathf.Approximately(launchAudio.spatialBlend, 1) && engineAudio.clip.channels == 1 && launchAudio.clip.channels == 1 && Vector3.Distance(engineAudio.transform.position, player.transform.position) < 1 && Vector3.Distance(launchAudio.transform.position, player.transform.position) < 1, "Mono engine sounds must follow the bike in full 3D");
                int activeListeners = 0;
                foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.enabled && listener.gameObject.activeInHierarchy) activeListeners++;
                Require(activeListeners == 1 && Camera.main != null && Camera.main.GetComponent<AudioListener>() != null && Camera.main.GetComponent<AudioListener>().enabled,
                    "Gameplay must have exactly one active listener on the main camera");
                var cruiseReverb = engineAudio.GetComponent<AudioReverbFilter>();
                var launchReverb = launchAudio.GetComponent<AudioReverbFilter>();
                Require(cruiseReverb != null && cruiseReverb.enabled && launchReverb != null && launchReverb.enabled,
                    "Both bike engine sources must have their own enabled reverb");
                CheckEngineReverbTuning(GameAudio.Active, audioPreset, cruiseReverb);
                var windFilter = windAudio.GetComponent<AudioLowPassFilter>();
                Require(windAudio.clip != null && windAudio.clip.channels == 2 && Mathf.Approximately(windAudio.spatialBlend, 0) && windFilter != null && windFilter.enabled,
                    "Speed wind must remain listener-relative stereo with its speed filter");
                if (audioPreset.FindProperty("windVolume").floatValue > 0)
                    Require(windAudio.volume > 0, "Configured wind must be audible while riding");
                var rushAudio = GameAudio.Active.transform.Find("Steering wind rush").GetComponent<AudioSource>();
                Require(rushAudio.clip != null && rushAudio.clip.channels == 2 && rushAudio.loop && rushAudio.spatialBlend == 0,
                    "Steering must have a separate recorded stereo air-rush layer");
                straightWindRush = rushAudio.volume;
                var jacketAudio = GameAudio.Active.JacketAudio;
                Require(jacketAudio != null && jacketAudio.clip != null && jacketAudio.clip.channels == 1 && jacketAudio.loop && jacketAudio.spatialBlend == 1,
                    "Jacket must have its own mono positional fabric recording");
                Require(jacketAudio.transform.IsChildOf(player.transform) && jacketAudio.transform.parent.name == "jacket_01_03" && jacketAudio.transform.localPosition == Vector3.zero,
                    "Fabric sound must be attached to the animated jacket hem");
                Require(jacketAudio.volume > 0 && jacketAudio.pitch > 1, "Fabric flutter must respond to riding speed");
                var pistolClips = audioPreset.FindProperty("shots");
                Require(pistolClips.arraySize == 1, "Weapon mix must replace the old shots with the new recorded 9mm discharge");
                var pistolRecording=pistolClips.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip;
                Require(pistolRecording!=null && pistolRecording.name=="Pistol9mm_Recorded" && pistolRecording.channels==1 && pistolRecording.loadState!=AudioDataLoadState.Failed,
                    $"The real 9mm pistol recording must be assigned and imported in mono: name={pistolRecording?.name}, channels={pistolRecording?.channels}, state={pistolRecording?.loadState}");
                CheckPistolMixPriority(GameAudio.Active);
                Require(audioPreset.FindProperty("passBys").GetArrayElementAtIndex(0).objectReferenceValue.name == "HighwayPass", "Traffic must use the replacement highway recording");
                Require(GameAudio.Active.EngineLevel > 0 && GameAudio.Active.EnginePitch > 0, "Engine layer must respond to bike speed");
                var randomBefore = UnityEngine.Random.state;
                SoundEvents.Emit(SoundCue.UiClick, Vector3.zero);
                Require(randomBefore.Equals(UnityEngine.Random.state), "Audio variation must not change gameplay randomness");
                audioBeforeShot = GameAudio.Active.PlayedCueCount;
                var passProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                passProbe.transform.position = player.transform.position + Vector3.forward * 40 + Vector3.right * 3;
                passProbe.AddComponent<Car>();
                // Probes start away from the road; a controlled scan later tests two fast passes.
                var farProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                farProbe.name = "Far whoosh probe";
                farProbe.transform.position = new Vector3(-5000, 100, -5000);
                farWhooshProbe = farProbe.AddComponent<Car>();
                var nearProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nearProbe.name = "Near whoosh probe";
                nearProbe.transform.position = new Vector3(-5001, 100, -5000);
                nearWhooshProbe = nearProbe.AddComponent<Car>();
                startZ = player.transform.position.z;
                startX = player.transform.position.x;
                var input = player.GetComponent<TouchInputManager>();
                input.SetAim(true); input.SetAim(true);
                Require(input.IsAiming, "Explicit aim-on must be idempotent");
                input.SetAim(false);
                Require(!input.IsAiming, "Explicit aim-off must clear aiming");
                foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentMethodName(i) == "SwitchAim") aimButton = button;
                }
                Require(aimButton != null, "The actual Aim button must be present");
                var muzzleObject = new SerializedObject(player).FindProperty("muzzleFlash").objectReferenceValue as GameObject;
                muzzle = muzzleObject.GetComponent<MuzzleFlash>();
                muzzleBefore = muzzle.SpawnCount;
                aimButton.onClick.Invoke();
                Require(input.IsAiming, "The scene Aim button must enable aiming despite its legacy false argument");
                CheckCarImpactCamera(Camera.main.GetComponent<CameraController>(),input);
                CheckAimCameraTuning(Camera.main.GetComponent<CameraController>(),player,input);
                aimPressedClock=EditorApplication.timeSinceStartup;
                input.setLeftPressed(true);
                steeringStarted = elapsed;

                var pickup = new GameObject("Collectible effect probe");
                pickupProbe = pickup;
                pickup.transform.position = player.transform.position;
                pickup.AddComponent<SphereCollider>().radius = 4;
                pickup.GetComponent<SphereCollider>().isTrigger = true;
                pickup.AddComponent<Collectible>();
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.position = new Vector3(5000, 100, 5000);
                target = box.AddComponent<Car>();
                var carSettings = new SerializedObject(target);
                Require(carSettings.FindProperty("destructible").boolValue && carSettings.FindProperty("explodeOnDeath").boolValue,
                    "Existing damage and explosion behavior must default to enabled");
                carSettings.FindProperty("health").intValue = 15;
                carSettings.ApplyModifiedPropertiesWithoutUndo();
                stage = 1;
            }
            if (stage == 1 && !whooshSelectionChecked && elapsed > 3) {
                CheckWhooshSelection(player);
                whooshSelectionChecked = true;
            }
            if(stage==1 && !steeringChecked) {
                var tire=player.GetComponent<CyberBiker.Player.TireContactParticles>();
                tireResponseSeen|=tire.CurrentEmissionRate>straightTireRate*1.5f && tire.ParticleCount>0;
            }
            if (stage == 1 && !steeringChecked && elapsed > steeringStarted + 0.65) {
                Require(player.transform.position.x < startX, "Input adapter must steer the motor");
                var tireParticles=player.GetComponent<CyberBiker.Player.TireContactParticles>();
                Require(tireResponseSeen,
                    "Steering must strengthen the grounded tire spray; rate="+tireParticles.CurrentEmissionRate+" baseline="+straightTireRate+" particles="+tireParticles.ParticleCount+" grounded="+tireParticles.GroundedWheelCount+" lean="+player.transform.GetChild(0).localEulerAngles.z+" steering="+player.xThrow+" state="+manager.gameState);
                Require(tireParticles.EmittedSparkCount>0,"A hard turn must emit skid-spark particles");
                CaptureTireParticles();
                Require(GameAudio.Active.transform.Find("Speed wind").GetComponent<AudioSource>().panStereo < -0.01f,
                    "Wind must subtly shift toward the steering side while moving");
                var turningRush = GameAudio.Active.transform.Find("Steering wind rush").GetComponent<AudioSource>();
                var windSettings = new SerializedObject(GameAudio.Active);
                if (windSettings.FindProperty("windVolume").floatValue > 0 && windSettings.FindProperty("steeringWindBoost").floatValue > 0.1f)
                    Require(turningRush.volume > straightWindRush * 1.5f && turningRush.panStereo < -0.01f,
                        "Steering must make the air-rush layer substantially louder and directional even at full wind volume");
                player.GetComponent<TouchInputManager>().setLeftPressed(false);
                steeringChecked = true;
            }
            if(stage==1 && firstPistolShotSeconds<0 && muzzle.SpawnCount>muzzleBefore) {
                firstPistolShotSeconds=EditorApplication.timeSinceStartup-aimPressedClock;
                Require(firstPistolShotSeconds<.6,"Ordinary Aim must draw, align the arm rig and fire promptly: "+firstPistolShotSeconds.ToString("F3")+" real seconds");
                File.WriteAllText("Logs/pistol-first-shot-latency.txt",firstPistolShotSeconds.ToString("F3")+" real seconds from ordinary Aim to first muzzle effect.");
            }
            if (stage == 1 && steeringChecked && elapsed > 6) {
                var traffic = UnityEngine.Object.FindFirstObjectByType<TrafficSpawner>();
                Require(traffic!=null,"Traffic spawner must remain present");
                if(traffic.BufferedWaveCount!=traffic.TargetWaveCount){Require(elapsed<8,$"Traffic did not refill: {traffic.BufferedWaveCount}/{traffic.TargetWaveCount}");return;}
                Require(GameAudio.Active.PlayedCueCount > audioBeforeShot, "Gameplay shots and pickups must produce audio cues");
                Require(GameAudio.Active.PassByCount > 0, "A nearby car crossing the player must produce a directional pass-by");
                Require(GameAudio.Active.WhooshCount > 0 && whooshSelectionChecked, "Fast close passes must trigger the independent air whoosh");
                Require(muzzle.SpawnCount > muzzleBefore, "Aim must reach the animation-ready automatic firing path and spawn muzzle effects");
                Require(firstPistolShotSeconds>=0,"Ordinary Aim first-shot latency must be measured");
                aimButton.onClick.Invoke();
                Require(!player.GetComponent<TouchInputManager>().IsAiming, "The scene Aim button must toggle off");
                Require(traffic != null && traffic.BufferedWaveCount == traffic.TargetWaveCount,
                    $"Traffic buffer must stay full: {traffic?.BufferedWaveCount}/{traffic?.TargetWaveCount}, horizon {traffic?.FurthestCarDistance}");
                File.WriteAllText("Logs/traffic-regression-check.txt", $"waves={traffic.BufferedWaveCount}/{traffic.TargetWaveCount}, horizon={traffic.FurthestCarDistance:F1}m");
                Require(pickupProbe == null, "A full-health player must still collect the pickup");
                Require(player.Health.Current == 100, "Collectibles must not change health");
                player.TakeDamage(new DamageInfo(25));
                Require(player.Health.Current == 75, "Scene damage must reach health");
                Require(player.transform.position.z > startZ, "Motor must move the player forward");
                Require(manager.score > 0 && manager.Session.SampleCount > 0, "Movement must accrue score and history");
                Require(target != null && target.IsAlive, "Damage target must initialize");
                target.enabled = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab");
                Require(target.CurrentHealth == 15, "Inspector health must configure the spawned car");
                tuningWeapon = new ProjectileWeapon(prefab, 0.2f, 20);
                tuningWeapon.SetDamage(6);
                Require(tuningWeapon.TryFire(new ShotRequest(target.transform.position - Vector3.forward * 3, Vector3.forward, null), Time.time),
                    "Configured probe weapon must fire");
                scoreBeforePause = manager.score;
                // Start a shake immediately before pausing so the pause check verifies
                // a live envelope, independent of how recently traffic passed.
                passCamera.TriggerPassShake(1f);
                shakeRemainingBeforePause = (float)typeof(CameraController).GetField("passShakeRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(passCamera);
                Require(shakeRemainingBeforePause > 0, "Camera shake must start before pausing");
                manager.OpenPauseMenu();
                pausedTreadPhase = player.GetComponent<CyberBiker.Player.TireTreadMotion>().TreadPhase;
                pausedTireEmissions=player.GetComponent<CyberBiker.Player.TireContactParticles>().EmittedCount;
                var pausedRush = GameAudio.Active.transform.Find("Steering wind rush").GetComponent<AudioSource>();
                pausedWindRush = pausedRush.volume; pausedWindPan = pausedRush.panStereo;
                pausedJacketVolume = GameAudio.Active.JacketAudio.volume;
                pausedJacketPitch = GameAudio.Active.JacketAudio.pitch;
                Require(Time.timeScale == 0, "Pause must freeze time");
                Require(GameAudio.Active.IsPaused, "Pause must suspend engine, wind and gameplay audio");
                Require(!GameAudio.Active.transform.Find("Bike engine cruise").GetComponent<AudioReverbFilter>().enabled &&
                    !GameAudio.Active.transform.Find("Bike engine launch").GetComponent<AudioReverbFilter>().enabled,
                    "Engine reverb must suspend while paused");
                engineElapsedBeforePause = GameAudio.Active.EngineLaunchElapsed;
                whooshCountBeforePause = GameAudio.Active.WhooshCount;
                nearWhooshProbe.transform.position = player.transform.position + Vector3.forward * 20;
                stage = 2;
            }
            if (stage == 2 && !pauseCrossingPlaced && elapsed > 6.8) {
                nearWhooshProbe.transform.position = player.transform.position - Vector3.forward * 20;
                pauseCrossingPlaced = true;
            }
            if (stage == 2 && pauseCrossingPlaced && elapsed > 7) {
                Require(GameAudio.Active.WhooshCount == whooshCountBeforePause, "Passing-air detection must not fire while paused");
                Require(player.GetComponent<CyberBiker.Player.TireTreadMotion>().TreadPhase == pausedTreadPhase,
                    "Pause must freeze the tread animation");
                Require(player.GetComponent<CyberBiker.Player.TireContactParticles>().EmittedCount==pausedTireEmissions,
                    "Pause must stop tire particle emission");
                var frozenRush = GameAudio.Active.transform.Find("Steering wind rush").GetComponent<AudioSource>();
                Require(frozenRush.volume == pausedWindRush && frozenRush.panStereo == pausedWindPan,
                    "Pause must freeze wind-rush gain and steering pan");
                Require(GameAudio.Active.JacketAudio.volume == pausedJacketVolume && GameAudio.Active.JacketAudio.pitch == pausedJacketPitch,
                    "Pause must freeze jacket flutter playback parameters");
                float shakeDuringPause = (float)typeof(CameraController).GetField("passShakeRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(passCamera);
                Require(Mathf.Approximately(shakeDuringPause, shakeRemainingBeforePause), "Camera shake must freeze while paused");
                Require(manager.score == scoreBeforePause, "Score must stay frozen during pause");
                Require(GameAudio.Active.EngineLaunchElapsed == engineElapsedBeforePause && GameAudio.Active.EngineLaunchCount == 1, "Pause must preserve launch progress");
                manager.ResumeGame(); stage = 3;
            }
            if (stage == 3 && elapsed > 11) {
                Require(manager.gameState == GameState.Playing && Time.timeScale == 1, "Countdown must resume gameplay");
                Require(!GameAudio.Active.IsPaused, "Resume must restore gameplay audio");
                Require(GameAudio.Active.transform.Find("Bike engine cruise").GetComponent<AudioReverbFilter>().enabled &&
                    GameAudio.Active.transform.Find("Bike engine launch").GetComponent<AudioReverbFilter>().enabled,
                    "Resume must restore engine reverb");
                Require(GameAudio.Active.EngineLaunchCount == 1, "Resume must not restart the engine launch");
                // Suppress unrelated near passes briefly so decay can be checked deterministically.
                var audioSettings = new SerializedObject(GameAudio.Active);
                whooshRadiusBeforeDecay = audioSettings.FindProperty("whooshDistance").floatValue;
                audioSettings.FindProperty("whooshDistance").floatValue = 0.0001f;
                audioSettings.ApplyModifiedPropertiesWithoutUndo();
                passCamera.TriggerPassShake(1f);
                Require(target.IsAlive && target.CurrentHealth == 9, "Tuned projectile must deal exactly 6 damage to a 15 HP car");
                var carSettings = new SerializedObject(target);
                carSettings.FindProperty("destructible").boolValue = false;
                carSettings.ApplyModifiedPropertiesWithoutUndo();
                target.TakeDamage(new DamageInfo(100));
                Require(target.CurrentHealth == 9, "Inspector destructible toggle must block damage");
                carSettings.FindProperty("destructible").boolValue = true;
                carSettings.FindProperty("explodeOnDeath").boolValue = false;
                carSettings.ApplyModifiedPropertiesWithoutUndo();
                tuningWeapon.SetDamage(9);
                Require(tuningWeapon.TryFire(new ShotRequest(target.transform.position - Vector3.forward * 3, Vector3.forward, null), Time.time),
                    "Live damage tuning must permit a second shot");
                var pistolSettings = new SerializedObject(manager);
                pistolSettings.FindProperty("pistolDamagePerShot").floatValue = 7;
                pistolSettings.FindProperty("secondsBetweenPistolShots").floatValue = 0.01f;
                pistolSettings.ApplyModifiedPropertiesWithoutUndo();
                var playerWeapon=(Weapon)typeof(PlayerController).GetField("weapon",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);
                var shotClock=typeof(Weapon).GetField("lastShotTime",BindingFlags.Instance|BindingFlags.NonPublic);
                shotClock.SetValue(playerWeapon,Time.time-.02f);
                int effectsBefore = muzzle.SpawnCount;
                player.Shoot();
                Require(muzzle.SpawnCount == effectsBefore + 1, "Live Inspector weapon tuning must preserve firing effects");
                bool foundTunedBullet = false;
                foreach (var bullet in UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None)) {
                    var settings = new SerializedObject(bullet);
                    if (settings.FindProperty("damage").floatValue == 7) foundTunedBullet = true;
                }
                Require(foundTunedBullet, "GameplayManager pistol damage must reach the next real projectile");
                player.Shoot();
                Require(muzzle.SpawnCount==effectsBefore+1,"Global pistol interval must still block a second shot in the same frame");
                pistolSettings.FindProperty("pistolDamagePerShot").floatValue = 15;
                pistolSettings.FindProperty("secondsBetweenPistolShots").floatValue = 0.45f;
                pistolSettings.ApplyModifiedPropertiesWithoutUndo();
                shotClock.SetValue(playerWeapon,Time.time-.1f);player.Shoot();
                Require(muzzle.SpawnCount==effectsBefore+1,"Restoring the global 0.45s interval must immediately enforce the longer cooldown");
                shotClock.SetValue(playerWeapon,Time.time-.46f);player.Shoot();
                Require(muzzle.SpawnCount==effectsBefore+2,"The global pistol cooldown must allow firing after 0.45s");
                stage = 4;
            }
            if (stage == 4 && elapsed > 13) {
                float shakeAfterDecay = (float)typeof(CameraController).GetField("passShakeRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(passCamera);
                Require(Mathf.Approximately(shakeAfterDecay, 0) && Mathf.Approximately(passCamera.PassShakeAmount, 0), "Camera shake must decay after the close pass");
                var audioSettings = new SerializedObject(GameAudio.Active);
                audioSettings.FindProperty("whooshDistance").floatValue = whooshRadiusBeforeDecay;
                audioSettings.ApplyModifiedPropertiesWithoutUndo();
                Require(target != null && !target.IsAlive, "Swept projectile must damage a static collider through IDamageable");
                player.TakeDamage(new DamageInfo(1000));
                Require(manager.gameState == GameState.GameOver && !player.IsAlive, "Death must end the run");
                manager.SwitchGameState(GameState.Playing);
                Require(manager.gameState == GameState.GameOver, "Recovery must not resurrect a dead run");
                player.enabled = false;
                player.GetComponent<Rigidbody>().isKinematic = true;
                player.transform.position += Vector3.forward * 600;
                stage = 5;
            }
            if (stage == 5 && elapsed > 15) {
                var traffic = UnityEngine.Object.FindFirstObjectByType<TrafficSpawner>();
                Require(traffic.BufferedWaveCount == traffic.TargetWaveCount, $"Traffic must refill after passing waves: {traffic.BufferedWaveCount}/{traffic.TargetWaveCount}");
                player.transform.position += Vector3.forward * 600;
                stage = 6;
            }
            if (stage == 6 && elapsed > 17) {
                var traffic = UnityEngine.Object.FindFirstObjectByType<TrafficSpawner>();
                if(traffic.BufferedWaveCount!=traffic.TargetWaveCount){Require(elapsed<20,$"Traffic did not refill after advancing: {traffic.BufferedWaveCount}/{traffic.TargetWaveCount}");return;}
                Require(traffic.BufferedWaveCount == traffic.TargetWaveCount && traffic.FurthestCarDistance > 100,
                    $"Traffic must preserve its forward buffer after repeated advances: {traffic.BufferedWaveCount}/{traffic.TargetWaveCount}, horizon {traffic.FurthestCarDistance}");
                File.AppendAllText("Logs/traffic-regression-check.txt", $"\nafter 1200m advance: waves={traffic.BufferedWaveCount}/{traffic.TargetWaveCount}, horizon={traffic.FurthestCarDistance:F1}m");
                Finish(true, "PASS: scene initialization, movement, damage, score/history, pause/countdown, real Aim button toggle, steering while aiming, automatic fire/muzzle effects, traffic buffer/refill over 1200m, full-health collectible without healing, Inspector health/destruction toggles, live rate/damage tuning, swept projectile damage, death and recovery guard, downloaded audio bindings, speed mix, simultaneous nearest pass detection, spatial whoosh reverb, directional camera roll and Aim stability, pause/resume and shake decay, independent audio randomness.");
            }
            if (elapsed > 40) Finish(false, "Smoke check timed out at stage " + stage);
        } catch (Exception e) { Finish(false, e.ToString()); }
    }
    private static void CheckPistolMixPriority(GameAudio rig) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var update=typeof(GameAudio).GetMethod("UpdatePistolDucking",flags);
        var apply=typeof(GameAudio).GetMethod("ApplyRidingLevels",flags);
        var fieldNames=new[]{"pistolDuckElapsed","pistolDuckGain","pistolDuckActive"};
        var saved=new object[fieldNames.Length];
        for(int i=0;i<saved.Length;i++)saved[i]=typeof(GameAudio).GetField(fieldNames[i],flags).GetValue(rig);
        var sources=new[]{rig.transform.Find("Bike engine launch").GetComponent<AudioSource>(),rig.transform.Find("Bike engine cruise").GetComponent<AudioSource>(),
            rig.transform.Find("Speed wind").GetComponent<AudioSource>(),rig.transform.Find("Steering wind rush").GetComponent<AudioSource>()};
        var original=new float[sources.Length];for(int i=0;i<sources.Length;i++)original[i]=sources[i].volume;
        float pitch=sources[1].pitch;
        try {
            SoundEvents.Emit(SoundCue.EnemyShot,Vector3.zero);
            Require(rig.PistolDuckingGain==1,"Enemy gunfire must not lower the player's engine or wind");
            SoundEvents.Emit(SoundCue.PlayerShot,Vector3.zero);
            float dipped=rig.PistolDuckingGain;
            var preset=new SerializedObject(rig);
            if(preset.FindProperty("pistolDuckDb").floatValue>0)Require(dipped>0 && dipped<1,"Player gunfire must immediately make room in the riding mix");
            for(int i=0;i<sources.Length;i++)Require(Mathf.Approximately(sources[i].volume,original[i]*dipped),"The shot must lower both engine layers and both wind layers immediately");
            SoundEvents.Emit(SoundCue.PlayerShot,Vector3.zero);
            for(int i=0;i<sources.Length;i++)Require(Mathf.Approximately(sources[i].volume,original[i]*dipped),"Repeated shots must not compound background attenuation");
            bool highPriority=false;
            foreach(var source in rig.GetComponentsInChildren<AudioSource>())if(source.clip!=null && source.clip.name=="Pistol9mm_Recorded" && source.priority<80)highPriority=true;
            Require(highPriority,"Player shots must have higher playback priority than ordinary effects");
            update.Invoke(rig,new object[]{0f});apply.Invoke(rig,null);
            Require(rig.PistolDuckingGain==dipped,"A paused or zero-time update must preserve the shot mix envelope");
            float hold=preset.FindProperty("pistolDuckHold").floatValue,release=preset.FindProperty("pistolDuckRelease").floatValue;
            update.Invoke(rig,new object[]{hold+release*.5f});apply.Invoke(rig,null);
            float recovering=rig.PistolDuckingGain;
            if(dipped<1)Require(recovering>dipped && recovering<1,"Engine and wind must recover smoothly instead of snapping back");
            update.Invoke(rig,new object[]{1f});apply.Invoke(rig,null);
            Require(rig.PistolDuckingGain==1,"The shot's mix dip must fully recover before the next normal pistol shot");
            for(int i=0;i<sources.Length;i++)Require(Mathf.Approximately(sources[i].volume,original[i]),"Shot recovery must restore the underlying riding volumes");
            Require(sources[1].pitch==pitch,"Pistol mix priority must not change the engine's pitch or speed response");
            File.WriteAllText("Logs/pistol-mix-check.txt",$"PASS: player-only mix dip {dipped:F3}, smooth recovery {recovering:F3} -> 1.000, no cumulative attenuation, zero-time freeze, original background levels/pitch restored, player voice priority above other effects.");
        } finally {
            for(int i=0;i<saved.Length;i++)typeof(GameAudio).GetField(fieldNames[i],flags).SetValue(rig,saved[i]);
            apply.Invoke(rig,null);
            foreach(var source in rig.GetComponentsInChildren<AudioSource>())if(source.clip!=null && source.clip.name=="Pistol9mm_Recorded")source.Stop();
        }
    }
    private static void CheckEngineReverbTuning(GameAudio rig, SerializedObject preset, AudioReverbFilter filter) {
        var amount = preset.FindProperty("engineReverbAmount");
        float original = amount.floatValue;
        var configure = typeof(GameAudio).GetMethod("ConfigureEngineReverb", BindingFlags.Instance | BindingFlags.NonPublic);
        amount.floatValue = 0.1f;
        preset.ApplyModifiedPropertiesWithoutUndo();
        configure.Invoke(rig, new object[] { filter });
        float quietLevel = filter.reverbLevel, shortDecay = filter.decayTime;
        amount.floatValue = 1f;
        preset.ApplyModifiedPropertiesWithoutUndo();
        configure.Invoke(rig, new object[] { filter });
        Require(filter.reverbLevel - quietLevel >= 1000 && filter.decayTime > shortDecay,
            "Engine reverb tuning must change both effect level and decay");
        amount.floatValue = original;
        preset.ApplyModifiedPropertiesWithoutUndo();
        configure.Invoke(rig, new object[] { filter });
    }
    private static void CheckWhooshSelection(PlayerController player) {
        Require(nearWhooshProbe != null && nearWhooshProbe.IsAlive && farWhooshProbe != null && farWhooshProbe.IsAlive,
            "Whoosh probes must initialize");
        var rig = GameAudio.Active;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(GameAudio);
        var positions = (Dictionary<Car, Vector3>)type.GetField("relativePositions", flags).GetValue(rig);
        var oldPositions = new Dictionary<Car, Vector3>(positions);
        var lastScanField = type.GetField("lastTrafficScan", flags);
        var nextPassField = type.GetField("nextPass", flags);
        var nextWhooshField = type.GetField("nextWhoosh", flags);
        float oldLastScan = (float)lastScanField.GetValue(rig), oldNextPass = (float)nextPassField.GetValue(rig), oldNextWhoosh = (float)nextWhooshField.GetValue(rig);
        var preset = new SerializedObject(rig);
        float radius = preset.FindProperty("whooshDistance").floatValue;
        Require(radius > 0 && preset.FindProperty("passWhooshVolume").floatValue > 0, "Whoosh must be configured for the check");
        positions.Clear();
        positions[farWhooshProbe] = new Vector3(radius * 0.7f, 0, 20);
        positions[nearWhooshProbe] = new Vector3(radius * 2.8f, 0, 20);
        farWhooshProbe.transform.position = player.transform.position + new Vector3(radius * 0.7f, 0, -20);
        // This probe ends outside the radius, but was close at the interpolated crossing.
        nearWhooshProbe.transform.position = player.transform.position + new Vector3(-radius * 2.4f, 0, -20);
        lastScanField.SetValue(rig, Time.time - 0.1f);
        nextWhooshField.SetValue(rig, 0f);
        nextPassField.SetValue(rig, Time.time + 60f); // traffic cooldown must not suppress the whoosh
        int before = rig.WhooshCount;
        passCamera = Camera.main.GetComponent<CameraController>();
        Require(passCamera != null, "Main camera must have its controller");
        int shakeBefore = passCamera.PassShakeCount;
        type.GetMethod("ScanTraffic", flags).Invoke(rig, null);
        Require(passCamera.PassShakeCount == shakeBefore + 1, "A close pass must trigger camera shake");
        var cameraType = typeof(CameraController);
        var targetRollField = cameraType.GetField("passShakeTargetRollSign", flags);
        var rollField = cameraType.GetField("passShakeRollSign", flags);
        Require((float)targetRollField.GetValue(passCamera) < 0, "A pass on the right must roll the camera left");
        var scrape=player.GetComponent<CyberBiker.Player.CollisionScrapeParticles>();
        Require(player.GetComponent<TouchInputManager>().IsAiming,
            $"Camera shake must preserve Aim input; state={UnityEngine.Object.FindAnyObjectByType<GameplayManager>().gameState}, player={player.transform.position}, wall={player.wallDirection}, scrape point={scrape?.LastContactPoint}, normal={scrape?.LastContactNormal}, impacts={scrape?.ImpactCount}");
        CheckSpeedShake(passCamera);
        var aimPosition = passCamera.transform.position;
        var aimRotation = passCamera.transform.rotation;
        typeof(CameraController).GetMethod("ApplyPassShake", flags).Invoke(passCamera, null);
        Require(passCamera.PassShakeAmount > 0, "Aim pose check must exercise an active shake");
        Require(Vector3.Distance(passCamera.transform.position, aimPosition) > 0.00001f && Quaternion.Angle(passCamera.transform.rotation, aimRotation) > 0.001f,
            "Close-pass shake must move and roll the camera while aiming");
        passCamera.transform.SetPositionAndRotation(aimPosition,aimRotation);
        float rollBeforeOppositePass = (float)rollField.GetValue(passCamera);
        passCamera.TriggerPassShake(1f, 1f);
        Require((float)targetRollField.GetValue(passCamera) > 0 && Mathf.Approximately((float)rollField.GetValue(passCamera), rollBeforeOppositePass),
            "An opposite-side pass must change the target roll without instantly snapping the current roll");
        Require(rig.WhooshCount == before + 1, "Two fast crossings in one scan must produce one nearest whoosh despite traffic cooldown");
        var anchors = (Transform[])type.GetField("voiceAnchors", flags).GetValue(rig);
        var voices = (AudioSource[])type.GetField("voices", flags).GetValue(rig);
        AudioSource selected = null;
        for (int i = 0; i < anchors.Length; i++) if (anchors[i] == nearWhooshProbe.transform) selected = voices[i];
        Require(selected != null && selected.clip == preset.FindProperty("passWhoosh").objectReferenceValue && selected.spatialBlend > 0,
            "The nearest interpolated crossing must receive the directional whoosh");
        var reverb = selected.GetComponent<AudioReverbFilter>();
        Require(reverb != null && reverb.enabled, "The whoosh source must have its own enabled reverb");
        positions.Clear();
        foreach (var pair in oldPositions) positions[pair.Key] = pair.Value;
        lastScanField.SetValue(rig, oldLastScan);
        nextPassField.SetValue(rig, oldNextPass);
        nextWhooshField.SetValue(rig, oldNextWhoosh);
        farWhooshProbe.transform.position = new Vector3(-5000, 100, -5000);
        nearWhooshProbe.transform.position = new Vector3(-5001, 100, -5000);
    }
    private static void CheckAimCameraTuning(CameraController camera,PlayerController player,TouchInputManager input) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(CameraController);var fields=type.GetFields(flags);var values=new object[fields.Length];
        for(int i=0;i<fields.Length;i++) values[i]=fields[i].GetValue(camera);
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        var view=camera.GetComponent<Camera>();float fov=view.fieldOfView,steering=player.xThrow;
        bool aiming=input.IsAiming;
        var late=type.GetMethod("LateUpdate",flags);var rotate=type.GetMethod("ProcessRotation",flags);
        var settings=new SerializedObject(camera);
        void Set(string name,float value) {settings.Update();settings.FindProperty(name).floatValue=value;settings.ApplyModifiedPropertiesWithoutUndo();}
        try {
            input.SetAim(true);player.xThrow=1;
            foreach(var name in new[]{"passShakePosition","passShakeRotation","carImpactPosition","carImpactRotation","speedShakePosition","speedShakeRotation"}) Set(name,0);
            Set("smoothness",1000);Set("aimXShiftOffset",0);Set("aimYShiftOffset",0);Set("aimForwardShiftOffset",0);Set("aimAngleShiftOffset",0);
            type.GetField("sideOffset",flags).SetValue(camera,1f);
            type.GetField("rotationOffset",flags).SetValue(camera,0f);
            type.GetField("cameraXangle",flags).SetValue(camera,0f);
            type.GetField("cameraYangle",flags).SetValue(camera,0f);
            camera.transform.rotation=Quaternion.identity;
            late.Invoke(camera,null);var neutral=camera.transform.position;
            Set("aimXShiftOffset",-3);late.Invoke(camera,null);var left=camera.transform.position;
            Set("aimXShiftOffset",3);late.Invoke(camera,null);var right=camera.transform.position;
            Require(Vector3.Dot(right-left,Vector3.right)>5.9f,"Aim side-shift Inspector tuning must change the actual camera position in both directions");
            Set("xShiftOffset",99);late.Invoke(camera,null);
            Require(Vector3.Distance(right,camera.transform.position)<.001f,"Riding side-shift tuning must not override the independent Aim shift");
            Set("aimXShiftOffset",0);Set("aimYShiftOffset",1);Set("aimForwardShiftOffset",2);late.Invoke(camera,null);
            Require(Mathf.Abs(camera.transform.position.y-neutral.y)> .9f && Mathf.Abs(camera.transform.position.z-neutral.z)>1.9f,
                "Aim height and forward-shift settings must both affect the rendered camera pose");
            type.GetField("fov",flags).SetValue(camera,40f);Set("aimFov",25);
            for(int i=0;i<8;i++) late.Invoke(camera,null);
            float narrow=view.fieldOfView;
            Set("aimFov",65);for(int i=0;i<8;i++) late.Invoke(camera,null);
            Require(narrow<40 && view.fieldOfView>narrow && view.fieldOfView<=65,"Live Aim FOV tuning must smoothly narrow and widen the camera view");
            Set("aimAngleShiftOffset",15);type.GetField("rotationOffset",flags).SetValue(camera,0f);rotate.Invoke(camera,null);
            float positive=Mathf.DeltaAngle(0,camera.transform.eulerAngles.z);
            Set("angleShiftOffset",-99);type.GetField("rotationOffset",flags).SetValue(camera,0f);rotate.Invoke(camera,null);
            Require(positive>.001f && Mathf.Abs(Mathf.DeltaAngle(positive,camera.transform.eulerAngles.z))<.01f,
                "Aim lean must use its own Inspector setting independently of riding lean");
            player.xThrow=-1;type.GetField("rotationOffset",flags).SetValue(camera,0f);rotate.Invoke(camera,null);
            Require(Mathf.DeltaAngle(0,camera.transform.eulerAngles.z)<-.001f,"Opposite steering must lean the aiming camera in the opposite direction");
            CheckDynamicAimShoulder(camera,player);
        } finally {
            for(int i=0;i<fields.Length;i++) fields[i].SetValue(camera,values[i]);
            camera.transform.SetPositionAndRotation(position,rotation);view.fieldOfView=fov;
            player.xThrow=steering;input.SetAim(aiming);
        }
    }
    private static void CheckDynamicAimShoulder(CameraController camera,PlayerController player) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(CameraController);
        var update=type.GetMethod("UpdateAimShoulder",flags);var late=type.GetMethod("LateUpdate",flags);
        var body=player.GetComponent<Rigidbody>();var position=body.position;var rotation=body.rotation;
        var settings=new SerializedObject(camera);GameObject shiftedRoad=null;
        void Place(float x) {var p=new Vector3(x,position.y,position.z);player.transform.position=p;body.position=p;Physics.SyncTransforms();}
        void Settle() {for(int i=0;i<60;i++) update.Invoke(camera,new object[]{.02f});late.Invoke(camera,null);}
        try {
            settings.FindProperty("dynamicAimShoulder").boolValue=true;
            settings.FindProperty("aimXShiftOffset").floatValue=0;
            settings.FindProperty("aimYShiftOffset").floatValue=0;
            settings.FindProperty("aimForwardShiftOffset").floatValue=0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            type.GetField("sideOffset",flags).SetValue(camera,0f);camera.transform.rotation=Quaternion.identity;
            float distance=Mathf.Abs(new SerializedObject(camera).FindProperty("aimOffset").vector3Value.x);
            var anchor=(Transform)type.GetField("target",flags).GetValue(camera);
            Require(distance>.1f,"The configured Aim offset must supply a shoulder distance");
            Place(-6);Settle();
            Require(camera.transform.position.x-anchor.position.x>distance*.8f,"The left side of the road must use the right camera shoulder");
            var leftFollow=(Vector3)type.GetField("offset",flags).GetValue(camera);
            Place(6);Settle();
            Require(camera.transform.position.x-anchor.position.x<-distance*.8f,"The right side of the road must use the left camera shoulder");
            Require(Vector3.Distance(leftFollow,(Vector3)type.GetField("offset",flags).GetValue(camera))<.001f,
                "Switching shoulders must not alter the camera's steering follow offset");
            settings.Update();settings.FindProperty("aimXShiftOffset").floatValue=-4;settings.ApplyModifiedPropertiesWithoutUndo();
            type.GetField("sideOffset",flags).SetValue(camera,1f);late.Invoke(camera,null);
            var steeringFollow=(Vector3)type.GetField("offset",flags).GetValue(camera);
            var rightSteered=camera.transform.position;
            type.GetField("sideOffset",flags).SetValue(camera,0f);late.Invoke(camera,null);
            var rightSteerDelta=rightSteered-camera.transform.position;
            type.GetField("sideOffset",flags).SetValue(camera,1f);Place(-6);
            for(int i=0;i<60;i++) {
                update.Invoke(camera,new object[]{.02f});late.Invoke(camera,null);
                Require(Vector3.Distance(steeringFollow,(Vector3)type.GetField("offset",flags).GetValue(camera))<.001f,
                    "A shoulder transition must leave held steering X shift fully active");
            }
            var leftSteered=camera.transform.position;
            type.GetField("sideOffset",flags).SetValue(camera,0f);late.Invoke(camera,null);
            Require(Vector3.Distance(rightSteerDelta,leftSteered-camera.transform.position)<.001f,
                "Steering X shift must have the same displacement on either shoulder");
            settings.Update();settings.FindProperty("aimXShiftOffset").floatValue=0;settings.ApplyModifiedPropertiesWithoutUndo();
            Place(6);Settle();
            for(int i=0;i<20;i++) {Place(i%2==0 ? -.3f : .3f);update.Invoke(camera,new object[]{.02f});}
            Require((float)type.GetField("aimShoulderSide",flags).GetValue(camera)==1,
                "Small centre-line corrections must retain the current shoulder without flickering");
            float before=(float)type.GetField("aimShoulderX",flags).GetValue(camera);
            float velocityBefore=(float)type.GetField("aimShoulderVelocity",flags).GetValue(camera);
            Place(-6);update.Invoke(camera,new object[]{0f});
            Require((float)type.GetField("aimShoulderX",flags).GetValue(camera)==before && (float)type.GetField("aimShoulderVelocity",flags).GetValue(camera)==velocityBefore,
                "Paused time must freeze the shoulder transition");
            update.Invoke(camera,new object[]{.02f});
            float after=(float)type.GetField("aimShoulderX",flags).GetValue(camera);
            float firstStep=before-after;
            Require(after<before && firstStep<distance*.03f,"Shoulder changes must start with a gentle glide instead of a large first-frame jump");
            update.Invoke(camera,new object[]{.02f});
            float next=(float)type.GetField("aimShoulderX",flags).GetValue(camera);
            Require(after-next>firstStep && after-next<distance*.08f,
                "The shoulder glide must gradually accelerate after its gentle start");
            shiftedRoad=GameObject.CreatePrimitive(PrimitiveType.Cube);shiftedRoad.name="Translated road shoulder check";
            shiftedRoad.layer=LayerMask.NameToLayer("Water");shiftedRoad.AddComponent<Tile>();
            shiftedRoad.transform.position=new Vector3(40,position.y+2,position.z);shiftedRoad.transform.localScale=new Vector3(20,1,100);
            Place(34);Settle();
            Require(camera.transform.position.x-anchor.position.x>distance*.8f,
                "Shoulder choice must use the current road centre, including roads away from world X zero");
            settings.Update();settings.FindProperty("dynamicAimShoulder").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
            Settle();
            Require(camera.transform.position.x-anchor.position.x>distance*.8f,"Disabling dynamic shoulders must restore the original fixed-right Aim offset");
        } finally {
            if(shiftedRoad!=null) UnityEngine.Object.DestroyImmediate(shiftedRoad);
            player.transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;Physics.SyncTransforms();
        }
    }
    private static void CheckCarImpactCamera(CameraController camera,TouchInputManager input) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var advance=typeof(CameraController).GetMethod("UpdateCarImpact",flags);
        var apply=typeof(CameraController).GetMethod("ApplyCarImpactPose",flags);
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        int count=camera.CarImpactCount;
        camera.TriggerCarImpact(1,-camera.transform.right);
        advance.Invoke(camera,new object[]{.04f});
        float amount=camera.CarImpactAmount;
        Require(camera.CarImpactCount==count+1 && amount>0,"Car impacts must start a smooth camera recoil envelope");
        advance.Invoke(camera,new object[]{0f});
        Require(camera.CarImpactAmount==amount,"Paused time must freeze car impact recoil");
        Vector3 forward=camera.transform.forward;
        apply.Invoke(camera,null);
        Require(Vector3.Distance(camera.transform.position,position)>.01f && Vector3.Angle(forward,camera.transform.forward)>.001f,
            "Aiming must show full positional and rotational collision recoil");
        Require(Quaternion.Angle(rotation,camera.transform.rotation)>.1f,"Aiming must still show the car impact roll");
        camera.transform.SetPositionAndRotation(position,rotation);
        advance.Invoke(camera,new object[]{2f});
        input.SetAim(false);
        Vector3 right=camera.transform.right;
        camera.TriggerCarImpact(1,right);advance.Invoke(camera,new object[]{.04f});apply.Invoke(camera,null);
        Require(Vector3.Dot(camera.transform.position-position,right)>.01f,"A side impact must kick the chase camera away from contact");
        camera.transform.SetPositionAndRotation(position,rotation);
        advance.Invoke(camera,new object[]{2f});
        camera.TriggerCarImpact(1,-camera.transform.forward);advance.Invoke(camera,new object[]{.04f});apply.Invoke(camera,null);
        Require(Vector3.Dot(camera.transform.position-position,forward)<-.01f,"A frontal impact must produce a backward camera recoil");
        advance.Invoke(camera,new object[]{2f});
        Require(camera.CarImpactAmount==0,"Car impact recoil must settle fully");
        camera.transform.SetPositionAndRotation(position,rotation);input.SetAim(true);
    }
    private static void CheckSpeedShake(CameraController camera) {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CameraController);
        var advance = type.GetMethod("UpdateSpeedShake", flags);
        var phase = type.GetField("speedShakePhase", flags);
        var amount = type.GetField("<SpeedShakeAmount>k__BackingField", flags);
        float oldPhase = (float)phase.GetValue(camera), oldAmount = camera.SpeedShakeAmount;
        var settings = new SerializedObject(camera);
        float start = settings.FindProperty("speedShakeStartSpeed").floatValue;
        float full = settings.FindProperty("speedShakeFullSpeed").floatValue;
        advance.Invoke(camera, new object[] { 0f, 2f });
        Require(camera.SpeedShakeAmount < 0.001f, "Camera vibration must settle at rest");
        advance.Invoke(camera, new object[] { Mathf.Lerp(start, full, 0.5f), 2f });
        float middle = camera.SpeedShakeAmount;
        advance.Invoke(camera, new object[] { full, 2f });
        Require(middle > 0 && middle < camera.SpeedShakeAmount && camera.SpeedShakeAmount <= 1,
            "Camera vibration must build with actual speed and stay bounded");
        float pausedAmount = camera.SpeedShakeAmount, pausedPhase = (float)phase.GetValue(camera);
        advance.Invoke(camera, new object[] { 0f, 0f });
        Require(camera.SpeedShakeAmount == pausedAmount && (float)phase.GetValue(camera) == pausedPhase,
            "Pause must freeze speed vibration strength and phase");
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        type.GetMethod("ApplySpeedShake", flags).Invoke(camera, null);
        Require(Vector3.Distance(position, camera.transform.position) > 0.000001f || Quaternion.Angle(rotation, camera.transform.rotation) > 0.001f,
            "Speed vibration must remain visible while aiming");
        camera.transform.SetPositionAndRotation(position,rotation);
        phase.SetValue(camera, oldPhase); amount.SetValue(camera, oldAmount);
    }
    private static void CheckTireParticleContact(PlayerController player,CyberBiker.Player.TireContactParticles particles) {
        var tick=typeof(CyberBiker.Player.TireContactParticles).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        var mesh=(Transform)typeof(CyberBiker.Player.TireContactParticles).GetField("motorcycle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(particles);
        var rotation=mesh.localRotation;
        var settings=new SerializedObject(particles);
        float start=settings.FindProperty("startLeanAngle").floatValue;
        float margin=settings.FindProperty("leanReleaseMargin").floatValue;
        float steering=player.xThrow;player.xThrow=1;
        mesh.localRotation=Quaternion.Euler(0,0,start-1);Physics.SyncTransforms();
        int initial=particles.EmittedCount;
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate==0 && particles.EmittedCount==initial,
            "Strong steering below the actual lean threshold must not emit smoke or sparks");
        player.xThrow=0;
        mesh.localRotation=Quaternion.Euler(0,0,start+1);Physics.SyncTransforms();
        var state=UnityEngine.Random.state;
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate>0,"Actual lean above the threshold must start smoke without requiring steering input");
        float gentleSparkRate=particles.CurrentSparkEmissionRate;
        Require(state.Equals(UnityEngine.Random.state),"Tire particles must not change gameplay randomness");
        mesh.localRotation=Quaternion.Euler(0,0,start-margin*.5f);Physics.SyncTransforms();
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate>0,"Lean near the start threshold must not flicker off");
        mesh.localRotation=Quaternion.Euler(0,0,start-margin-1);Physics.SyncTransforms();
        int beforeRelease=particles.EmittedCount;
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate==0 && particles.EmittedCount==beforeRelease,
            "Returning below the release angle must stop new smoke and sparks");
        mesh.localRotation=Quaternion.Euler(0,0,-start-1);Physics.SyncTransforms();
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate>0,"Both lean directions must activate smoke");
        mesh.localRotation=Quaternion.Euler(0,0,-settings.FindProperty("fullLeanAngle").floatValue-1);Physics.SyncTransforms();
        tick.Invoke(particles,null);
        float hardSparkRate=particles.CurrentSparkEmissionRate;
        Require(hardSparkRate>gentleSparkRate*4,"Deep lean must produce a substantially stronger spark shower");
        float boost=settings.FindProperty("hardLeanSparkBoost").floatValue;
        settings.FindProperty("hardLeanSparkBoost").floatValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
        tick.Invoke(particles,null);
        Require(Mathf.Approximately(hardSparkRate,particles.CurrentSparkEmissionRate*boost),
            "Inspector hard-lean spark boost must affect emission in an active run");
        settings.FindProperty("hardLeanSparkBoost").floatValue=boost;settings.ApplyModifiedPropertiesWithoutUndo();
        int emitted=particles.EmittedCount;var position=player.transform.position;
        player.transform.position+=Vector3.up*10;Physics.SyncTransforms();
        tick.Invoke(particles,null);
        Require(particles.GroundedWheelCount==0 && particles.EmittedCount==emitted,"Airborne wheels must not emit road particles");
        player.transform.position=position;Physics.SyncTransforms();
        var body=player.GetComponent<Rigidbody>();var velocity=body.linearVelocity;body.linearVelocity=Vector3.zero;
        tick.Invoke(particles,null);
        Require(particles.CurrentEmissionRate==0 && particles.EmittedCount==emitted,"Stationary tires must not emit spray");
        body.linearVelocity=velocity;
        mesh.localRotation=rotation;player.xThrow=steering;Physics.SyncTransforms();
        tick.Invoke(particles,null);
    }
    private static void CaptureTireParticles() {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera=Camera.main;var previous=camera.targetTexture;
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        var rider=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        camera.transform.position=rider.transform.position-Vector3.forward*8+Vector3.up*3;
        camera.transform.LookAt(rider.transform.position+Vector3.up*.8f);
        var rt=new RenderTexture(1080,720,24);camera.targetTexture=rt;camera.Render();
        var active=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1080,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1080,720),0,0);image.Apply();
        Directory.CreateDirectory("Logs/Tire");File.WriteAllBytes("Logs/Tire/contact-particles.png",image.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=previous;
        camera.transform.SetPositionAndRotation(position,rotation);
        UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
    }
    private static void Finish(bool passed, string message) {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/gameplay-smoke-check.txt", message);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
