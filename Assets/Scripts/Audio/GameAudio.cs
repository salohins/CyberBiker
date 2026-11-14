using System;
using System.Collections.Generic;
using CyberBiker.Combat;
using Gameplay;
using UnityEngine;

namespace CyberBiker.Audio {
    // Sound-only envelope: the physics system continues to own speed and acceleration.
    public sealed class ArcadeEngineSoundState {
        public bool HasStarted { get; private set; }
        public float LaunchElapsed { get; private set; }
        public float Speed { get; private set; }
        public float LaunchGain { get; private set; }
        public float CruiseGain { get; private set; }
        public float Pitch { get; private set; } = 1;

        public bool Update(float speed, float referenceSpeed, float dt, bool playing, bool running,
            float launchDuration, float crossfadeSeconds, float crossfadeEndOffsetSeconds, float riseSeconds, float fallSeconds,
            float pitchAtRest, float pitchAtTopSpeed) {
            if (dt <= 0) return false;
            bool start = !HasStarted && playing;
            if (start) HasStarted = true;
            float target = Mathf.Clamp01(speed / Mathf.Max(1, referenceSpeed));
            float response = target > Speed ? riseSeconds : fallSeconds;
            Speed = Mathf.MoveTowards(Speed, target, dt / Mathf.Max(0.01f, response));
            Pitch = Mathf.Lerp(pitchAtRest, pitchAtTopSpeed, Speed);
            if (!running || !HasStarted) { LaunchGain = 0; CruiseGain = 0; return start; }
            LaunchElapsed = Mathf.Min(Mathf.Max(0, launchDuration), LaunchElapsed + dt);
            // Finish before the launch clip's own tail fades or its AudioSource stops.
            float fadeEnd = Mathf.Max(0, launchDuration - Mathf.Max(0, crossfadeEndOffsetSeconds));
            float fade = Mathf.Min(Mathf.Max(0.01f, crossfadeSeconds), fadeEnd);
            float transition = fade > 0 ? Mathf.Clamp01((LaunchElapsed - (fadeEnd - fade)) / fade) : 1;
            float eased = Mathf.SmoothStep(0, 1, transition);
            // Equal-power mixing avoids the dip from a linear crossfade between unrelated recordings.
            LaunchGain = Mathf.Clamp01(Mathf.Cos(eased * Mathf.PI * 0.5f));
            CruiseGain = Mathf.Clamp01(Mathf.Sin(eased * Mathf.PI * 0.5f));
            return start;
        }
    }

    public sealed partial class GameAudio : MonoBehaviour {
        private readonly System.Random variation = new System.Random(8429);
        public static GameAudio Active { get; private set; }
        [Header("Mix - editable during Play mode")]
        [SerializeField, Range(0, 1)] private float masterVolume = 0.8f;
        [SerializeField, Range(0, 1)] private float engineVolume = 0.23f;
        [SerializeField, Range(0, 1)] private float windVolume = 0.16f;
        [SerializeField, Range(0, 1)] private float trafficVolume = 0.5f;
        [Tooltip("Cinematic rush of air when passing a nearby car.")]
        [SerializeField, Range(0, 1)] private float passWhooshVolume = 0.28f;
        [Tooltip("Reflections on passing-air sounds. Zero is dry; higher values add a longer, stronger tail.")]
        [SerializeField, Range(0, 1)] private float passWhooshReverbAmount = 0.4f;
        [SerializeField, Range(0, 1)] private float weaponVolume = 0.45f;
        [SerializeField, Range(0, 1)] private float impactVolume = 0.5f;
        [SerializeField, Range(0, 1)] private float explosionVolume = 0.6f;
        [SerializeField, Range(0, 1)] private float interfaceVolume = 0.3f;
        [Header("Finisher notifications")]
        [SerializeField, Range(0,1)] private float finisherReadyVolume = .8f;
        [SerializeField, Range(0,1)] private float finisherWarningVolume = .85f;
        [Tooltip("Brief engine/wind reduction for finisher alerts, in dB. Zero disables it.")]
        [SerializeField, Range(0,12)] private float finisherDuckDb = 5;
        [SerializeField] private AudioClip finisherReady;
        [SerializeField] private AudioClip finisherWarning;
        [Header("Player pistol mix priority")]
        [Tooltip("Brief engine/wind reduction when the player fires, in dB. Zero disables it.")]
        [SerializeField, Range(0,12)] private float pistolDuckDb = 6;
        [SerializeField, Range(0,.2f)] private float pistolDuckHold = .055f;
        [Tooltip("Seconds for engine and wind to smoothly recover after the shot's attack.")]
        [SerializeField, Range(.03f,.5f)] private float pistolDuckRelease = .16f;
        [Header("Speed and traffic")]
        [SerializeField, Min(1)] private float referenceSpeed = 100;
        [Tooltip("How much the bike engine follows its position relative to the camera.")]
        [SerializeField, Range(0, 1)] private float engineSpatialBlend = 1f;
        [Tooltip("Amount of short city reflections on the bike engine. Set to zero for a dry engine.")]
        [SerializeField, Range(0, 1)] private float engineReverbAmount = 0.3f;
        [Header("Engine response")]
        [SerializeField, Min(0.01f)] private float engineRiseSeconds = 4f;
        [SerializeField, Min(0.01f)] private float engineFallSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float engineCrossfadeSeconds = 2.5f;
        [Tooltip("Finish the engine transition this many seconds before the launch clip ends.")]
        [SerializeField, Min(0)] private float engineCrossfadeEndOffsetSeconds = 0.25f;
        [SerializeField, Range(0.5f, 2f)] private float cruisePitchAtRest = 0.85f;
        [SerializeField, Range(0.5f, 2f)] private float cruisePitchAtTopSpeed = 1.08f;
        [SerializeField, Min(1)] private float passByDistance = 25;
        [SerializeField, Min(1)] private float minimumRelativeSpeed = 8;
        [Tooltip("Maximum lateral distance for the passing-air effect.")]
        [SerializeField, Min(0.1f)] private float whooshDistance = 7f;
        [Tooltip("Minimum seconds between passing-air effects.")]
        [SerializeField, Min(0)] private float whooshInterval = 0.35f;
        [Tooltip("Side-to-side movement of wind as the rider leans.")]
        [SerializeField, Range(0, 0.8f)] private float steeringWindPan = 0.5f;
        [Tooltip("Strength of the brighter air rush exposed while leaning.")]
        [SerializeField, Range(0, 2f)] private float steeringWindBoost = 1f;
        [SerializeField, Range(0, 1)] private float windRushVolume = 0.65f;
        [Header("Jacket fabric")]
        [SerializeField, Range(0, 1)] private float jacketVolume = 0.35f;
        [SerializeField, Range(0, 2)] private float jacketSteeringBoost = 0.6f;
        [Tooltip("Animated hem rotation in degrees per second that produces full flutter intensity.")]
        [SerializeField, Min(1)] private float jacketMotionReference = 180f;
        [Tooltip("Wind brightness at low and high speeds, in Hz. The wind stays stereo around the rider.")]
        [SerializeField, Range(1000, 22000)] private float windLowSpeedCutoff = 9000f;
        [SerializeField, Range(1000, 22000)] private float windHighSpeedCutoff = 20000f;
        [SerializeField, Range(4, 32)] private int voiceCount = 16;
        [Header("Downloaded sound clips - replace freely")]
        [SerializeField] private AudioClip engineLaunch;
        [SerializeField] private AudioClip engineCruise;
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private AudioClip windRushLoop;
        [SerializeField] private AudioClip jacketFabricLoop;
        [SerializeField] private AudioClip passWhoosh;
        [SerializeField] private AudioClip[] shots;
        [SerializeField] private AudioClip[] passBys;
        [SerializeField] private AudioClip[] impacts;
        [SerializeField] private AudioClip[] explosions;
        [SerializeField] private AudioClip pickup;
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioClip pause;
        [SerializeField] private AudioClip resume;
        [SerializeField] private AudioClip gameOver;
        private AudioSource engineSource, launchSource, windSource, windRushSource, jacketSource;
        private AudioSource launchPowerSource;
        private AudioReverbFilter launchPowerReverb;
        private float launchPowerAmount,launchPowerVolume,launchEngineLiftDb,launchWindLiftDb;
        public AudioSource LaunchPowerAudio => launchPowerSource;
        public int LaunchBurstSoundCount { get; private set; }
        private float engineBaseLevel,launchBaseLevel,windBaseLevel,rushBaseLevel;
        private float pistolDuckElapsed,pistolDuckGain=1;
        private bool pistolDuckActive;
        private float swordDuckAge=100,swordDuckDb,swordDuckHold,swordDuckRelease=.32f,swordDuckGain=1;
        public float SwordDuckingGain => swordDuckGain;
        public int SwordSoundCount { get; private set; }
        public int SwordExplosionSoundCount { get; private set; }
        public float PistolDuckingGain => pistolDuckGain;
        private float finisherDuckAge=100,finisherDuckGain=1;
        public int FinisherReadyCueCount { get; private set; }
        public int FinisherWarningCueCount { get; private set; }
        private Transform jacketAnchor;
        private Quaternion previousJacketRotation;
        private float jacketFlutter;
        public AudioSource JacketAudio => jacketSource;
        private AudioReverbFilter engineReverb, launchReverb;
        private AudioLowPassFilter windLowPass;
        private readonly ArcadeEngineSoundState engineMix = new ArcadeEngineSoundState();
        private AudioSource[] voices;
        private AudioReverbFilter[] whooshReverbs;
        private bool[] whooshVoices;
        private float[] voiceGains;
        private Transform[] voiceAnchors;
        private bool[] suspended;
        private int nextVoice;
        private bool paused;
        private float nextScan, nextPass, nextWhoosh, nextImpact, windSteering;
        private float lastTrafficScan = -1;
        private PlayerController player;
        private CameraController passCamera;
        private GameplayManager manager;
        private Rigidbody bike;
        private Health boundHealth;
        private float previousHealth;
        private bool boundSession;
        private readonly Dictionary<Car, Vector3> relativePositions = new Dictionary<Car, Vector3>();
        private readonly List<Car> removed = new List<Car>();
        public int PlayedCueCount { get; private set; }
        public int PassByCount { get; private set; }
        public int WhooshCount { get; private set; }
        public bool IsPaused => paused;
        public float EnginePitch => engineMix.Pitch;
        public float EngineLaunchElapsed => engineMix.LaunchElapsed;
        public int EngineLaunchCount { get; private set; }
        public float EngineLevel => Mathf.Max(engineSource != null ? engineSource.volume : 0, launchSource != null ? launchSource.volume : 0);
        public int ConfiguredClipCount => (engineLaunch != null ? 1 : 0) + (engineCruise != null ? 1 : 0) + (windLoop != null ? 1 : 0) + (windRushLoop != null ? 1 : 0) + (jacketFabricLoop != null ? 1 : 0) + (passWhoosh != null ? 1 : 0) +
            shots.Length + passBys.Length + impacts.Length + explosions.Length +
            (pickup != null ? 1 : 0) + (uiClick != null ? 1 : 0) + (pause != null ? 1 : 0) + (resume != null ? 1 : 0) + (gameOver != null ? 1 : 0) +
            (finisherReady != null ? 1 : 0) + (finisherWarning != null ? 1 : 0);

        public static void EnsurePresent() {
            if (Active != null) return;
            var prefab = Resources.Load<GameAudio>("CyberBikerAudio");
            if (prefab != null) Instantiate(prefab);
        }
        private void Awake() {
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this;
            engineSource = CreateSource("Bike engine cruise", true);
            launchSource = CreateSource("Bike engine launch", false);
            engineSource.clip = engineCruise;
            launchSource.clip = engineLaunch;
            engineSource.minDistance = launchSource.minDistance = 12;
            engineSource.maxDistance = launchSource.maxDistance = 100;
            engineReverb = CreateEngineReverb(engineSource);
            launchReverb = CreateEngineReverb(launchSource);
            launchPowerSource=CreateSource("Launch high RPM layer",true);
            launchPowerSource.spatialBlend=1;launchPowerSource.minDistance=12;launchPowerSource.maxDistance=100;
            launchPowerSource.priority=130;launchPowerReverb=CreateEngineReverb(launchPowerSource);
            if (engineCruise != null) engineSource.Play();
            windSource = CreateSource("Speed wind", true);
            // Wind is listener-relative stereo, not a point source on the bike.
            windSource.spatialBlend = 0;
            windLowPass = windSource.gameObject.AddComponent<AudioLowPassFilter>();
            windLowPass.cutoffFrequency = Mathf.Clamp(windLowSpeedCutoff, 1000, 22000);
            windSource.clip = windLoop;
            if (windLoop != null) windSource.Play();
            windRushSource = CreateSource("Steering wind rush", true);
            windRushSource.spatialBlend = 0;
            windRushSource.clip = windRushLoop;
            if (windRushLoop != null) windRushSource.Play();
            jacketSource = CreateSource("Jacket fabric", true);
            jacketSource.clip = jacketFabricLoop;
            jacketSource.spatialBlend = 1;
            jacketSource.minDistance = 12; jacketSource.maxDistance = 45;
            if (jacketFabricLoop != null) jacketSource.Play();
            int count = Mathf.Clamp(voiceCount, 4, 32);
            voices = new AudioSource[count]; voiceGains = new float[count];
            voiceAnchors = new Transform[count]; suspended = new bool[count];
            whooshReverbs = new AudioReverbFilter[count]; whooshVoices = new bool[count];
            enemyArrivalWhooshVoices=new bool[count];
            for (int i = 0; i < count; i++) voices[i] = CreateSource("Effect " + i, false);
        }
        private AudioSource CreateSource(string label, bool loop) {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.dopplerLevel = 0; // Deliberate pass-by design avoids extreme Doppler at runner speeds.
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 5; source.maxDistance = 65;
            source.priority = loop ? 160 : 80;
            return source;
        }
        private AudioReverbFilter CreateEngineReverb(AudioSource source) {
            // Apply short reflections only to the engine. No global listener or UI effects.
            var filter = source.gameObject.AddComponent<AudioReverbFilter>();
            filter.reverbPreset = AudioReverbPreset.User;
            filter.dryLevel = 0;
            filter.room = -350;
            filter.roomHF = -1800;
            filter.decayHFRatio = 0.55f;
            filter.reflectionsDelay = 0.012f;
            filter.reverbDelay = 0.025f;
            ConfigureEngineReverb(filter);
            return filter;
        }
        private void ConfigureEngineReverb(AudioReverbFilter filter) {
            if (filter == null) return;
            float amount = Mathf.Clamp01(engineReverbAmount);
            filter.enabled = !paused && amount > 0.001f;
            if (!filter.enabled) return;
            float level = 2000 * Mathf.Log10(Mathf.Max(amount, 0.00001f));
            filter.decayTime = Mathf.Lerp(0.35f, 1.0f, amount);
            filter.reflectionsLevel = Mathf.Clamp(-300 + level, -10000, 1000);
            filter.reverbLevel = Mathf.Clamp(-150 + level, -10000, 2000);
        }
        private void ConfigureWhooshReverb(int index) {
            var filter = whooshReverbs[index];
            if (filter == null) return;
            float amount = Mathf.Clamp01(enemyArrivalWhooshVoices!=null && enemyArrivalWhooshVoices[index] ? enemyArrivalReverbAmount : passWhooshReverbAmount);
            filter.enabled = whooshVoices[index] && !paused && amount > 0.001f;
            if (!filter.enabled) return;
            float level = 2000 * Mathf.Log10(Mathf.Max(amount, 0.00001f));
            filter.decayTime = Mathf.Lerp(0.45f, 1.3f, amount);
            filter.reflectionsLevel = Mathf.Clamp(-300 + level, -10000, 1000);
            filter.reverbLevel = Mathf.Clamp(-150 + level, -10000, 2000);
        }
        private void OnEnable() { SoundEvents.Raised += OnSound; }
        private void OnDisable() { SoundEvents.Raised -= OnSound; }
        private void Update() {
            if (Active != this) return;
            if (player == null) {
                player = FindAnyObjectByType<PlayerController>();
                if (player != null) { bike = player.GetComponent<Rigidbody>(); BindJacket(); }
            }
            if (manager == null) manager = FindAnyObjectByType<GameplayManager>();
            if (!boundSession && manager != null && manager.Session != null) {
                manager.Session.StateChanged += OnStateChanged; boundSession = true;
                SetPaused(manager.gameState == GameState.Pause);
            }
            if (boundHealth == null && player != null && player.Health != null) {
                boundHealth = player.Health; previousHealth = boundHealth.Current; boundHealth.Changed += OnHealthChanged;
            }
            float speed = bike != null ? bike.linearVelocity.magnitude : 0;
            if (player != null) engineSource.transform.position = launchSource.transform.position = player.transform.position;
            engineSource.spatialBlend = launchSource.spatialBlend = engineSpatialBlend;
            ConfigureEngineReverb(engineReverb);
            ConfigureEngineReverb(launchReverb);
            ConfigureEngineReverb(launchPowerReverb);
            float normalized = Mathf.Clamp01(speed / Mathf.Max(1, referenceSpeed));
            bool running = manager != null && manager.gameState != GameState.GameOver;
            float dt = Time.unscaledDeltaTime;
            if(!paused) {UpdatePistolDucking(dt);UpdateSwordDucking(dt);UpdateFinisherDucking(dt);}
            bool start = engineMix.Update(speed, referenceSpeed, paused ? 0 : dt,
                player != null && manager != null && manager.gameState == GameState.Playing, running,
                engineLaunch != null ? engineLaunch.length : 0, engineCrossfadeSeconds, engineCrossfadeEndOffsetSeconds,
                engineRiseSeconds, engineFallSeconds, cruisePitchAtRest, cruisePitchAtTopSpeed);
            if (start && engineLaunch != null) { launchSource.Play(); EngineLaunchCount++; }
            engineSource.pitch = engineMix.Pitch;
            if (!paused) {
                float smoothing = 1 - Mathf.Exp(-dt * 8);
                launchBaseLevel = Mathf.Lerp(launchBaseLevel, masterVolume * engineVolume * engineMix.LaunchGain, smoothing);
                engineBaseLevel = Mathf.Lerp(engineBaseLevel, masterVolume * engineVolume * engineMix.CruiseGain * (0.65f + 0.35f * engineMix.Speed), smoothing);
            }
            float steering = player != null && manager != null && manager.gameState == GameState.Playing ? player.xThrow : 0;
            if (!paused) windSteering = Mathf.Lerp(windSteering, Mathf.Clamp(steering, -1, 1), 1 - Mathf.Exp(-dt * 9));
            windSource.panStereo = windSteering * steeringWindPan;
            windRushSource.panStereo = windSteering * steeringWindPan;
            if (!paused) {
                float targetCutoff = Mathf.Lerp(Mathf.Clamp(windLowSpeedCutoff, 1000, 22000),
                    Mathf.Clamp(windHighSpeedCutoff, 1000, 22000), normalized);
                targetCutoff = Mathf.Min(22000, targetCutoff + Mathf.Abs(windSteering) * 2500);
                windLowPass.cutoffFrequency = Mathf.Lerp(windLowPass.cutoffFrequency, targetCutoff, 1 - Mathf.Exp(-dt * 4));
                // Reserve headroom at full speed: the old single-source boost clipped
                // at volume 1, making steering inaudible when the wind slider was maxed.
                float speedPressure = Mathf.Pow(normalized, 1.5f);
                float windGain = running ? masterVolume * windVolume * speedPressure * 0.45f : 0;
                float rushGain = running ? masterVolume * windVolume * windRushVolume * speedPressure *
                    Mathf.Clamp01(0.15f + Mathf.Abs(windSteering) * steeringWindBoost) : 0;
                windBaseLevel = Mathf.Lerp(windBaseLevel, windGain, 1 - Mathf.Exp(-dt * 5));
                rushBaseLevel = Mathf.Lerp(rushBaseLevel, rushGain, 1 - Mathf.Exp(-dt * 9));
                windRushSource.pitch = 1 + Mathf.Abs(windSteering) * 0.06f;
            }
            ApplyRidingLevels();
            for (int i = 0; i < voices.Length; i++) {
                voices[i].volume = masterVolume * voiceGains[i];
                if (voiceAnchors[i] != null) voices[i].transform.position = voiceAnchors[i].position;
                if (whooshVoices[i]) ConfigureWhooshReverb(i);
            }
            if (!paused && running && Time.time >= nextScan) { ScanTraffic(); nextScan = Time.time + 0.05f; }
            UpdateEnemyArrivalAudio(dt);
        }
        private void UpdatePistolDucking(float dt) {
            if(!pistolDuckActive) {pistolDuckGain=1;return;}
            pistolDuckElapsed+=Mathf.Max(0,dt);
            float release=Mathf.Max(.03f,pistolDuckRelease);
            float recovery=Mathf.Clamp01((pistolDuckElapsed-Mathf.Max(0,pistolDuckHold))/release);
            float low=Mathf.Pow(10,-Mathf.Max(0,pistolDuckDb)/20);
            pistolDuckGain=Mathf.Lerp(low,1,Mathf.SmoothStep(0,1,recovery));
            if(recovery>=1)pistolDuckActive=false;
        }
        private void ApplyRidingLevels() {
            float gain=Mathf.Min(pistolDuckGain,Mathf.Min(swordDuckGain,finisherDuckGain));
            float engineLift=Mathf.Pow(10,launchEngineLiftDb*launchPowerAmount/20);
            float windLift=Mathf.Pow(10,launchWindLiftDb*launchPowerAmount/20);
            launchSource.volume=Mathf.Min(1,launchBaseLevel*engineLift*gain);
            engineSource.volume=Mathf.Min(1,engineBaseLevel*engineLift*gain);
            windSource.volume=Mathf.Min(1,windBaseLevel*windLift*gain);
            windRushSource.volume=Mathf.Min(1,rushBaseLevel*windLift*gain);
            if(launchPowerSource!=null)launchPowerSource.volume=masterVolume*engineVolume*launchPowerAmount*launchPowerVolume*gain;
        }
        public void SetLaunchMix(AudioClip clip,float amount,float volume,float pitch,float engineLiftDb,float windLiftDb) {
            launchPowerAmount=Mathf.Clamp01(amount);launchPowerVolume=Mathf.Clamp01(volume);
            launchEngineLiftDb=Mathf.Clamp(engineLiftDb,0,6);launchWindLiftDb=Mathf.Clamp(windLiftDb,0,6);
            if(launchPowerSource==null)return;
            if(launchPowerSource.clip!=clip){launchPowerSource.Stop();launchPowerSource.clip=clip;}
            if(player!=null)launchPowerSource.transform.position=player.transform.position;
            launchPowerSource.pitch=Mathf.Clamp(pitch,.5f,2);
            if(launchPowerAmount>.001f && clip!=null && !paused && !launchPowerSource.isPlaying)launchPowerSource.Play();
            else if(launchPowerAmount<=.001f)launchPowerSource.Stop();
            ApplyRidingLevels();
        }
        public void PlayLaunchBurst(AudioClip clip,Transform anchor,float gain,float pitch) {
            if(clip==null || anchor==null || paused || masterVolume<=0 || engineVolume<=0 || gain<=0)return;
            int index=Play(clip,anchor.position,gain*engineVolume,pitch,false,anchor,priority:36);
            if(index<0)return;
            voices[index].spatialBlend=.85f;voices[index].minDistance=12;voices[index].maxDistance=100;
            LaunchBurstSoundCount++;
        }
        public void DuckForSwordImpact(float decibels,float hold,float release) {
            if(paused)return;
            swordDuckAge=0;swordDuckDb=decibels;swordDuckHold=hold;swordDuckRelease=Mathf.Max(.03f,release);
            UpdateSwordDucking(0);ApplyRidingLevels();
        }
        private void UpdateSwordDucking(float dt) {
            swordDuckAge+=Mathf.Max(0,dt);
            float recovery=Mathf.Clamp01((swordDuckAge-swordDuckHold)/swordDuckRelease);
            swordDuckGain=Mathf.Lerp(Mathf.Pow(10,-Mathf.Max(0,swordDuckDb)/20),1,Mathf.SmoothStep(0,1,recovery));
        }
        public void PlaySwordSound(AudioClip clip,Vector3 position,Transform rider,float gain,float pitch) {
            if(clip==null || paused || masterVolume<=0 || weaponVolume<=0)return;
            // Keep the recorded decay with the rider at runner speeds. The pooled
            // voice still respects master/weapon volumes and pause/resume.
            int index=Play(clip,position,gain*weaponVolume,pitch,false,rider,priority:40);
            if(index<0)return;
            voices[index].spatialBlend=.45f;voices[index].minDistance=12;voices[index].maxDistance=100;
            SwordSoundCount++;
        }
        public void PlaySwordExplosion(AudioClip clip,Transform blast,float gain,float pitch) {
            if(clip==null || blast==null || paused || masterVolume<=0 || explosionVolume<=0)return;
            int index=Play(clip,blast.position,gain*explosionVolume,pitch,false,blast,priority:32);
            if(index<0)return;
            voices[index].spatialBlend=.7f;voices[index].minDistance=18;voices[index].maxDistance=140;
            SwordExplosionSoundCount++;
        }
        private void UpdateFinisherDucking(float dt) {
            finisherDuckAge+=Mathf.Max(0,dt);
            float recovery=Mathf.SmoothStep(0,1,Mathf.Clamp01((finisherDuckAge-.12f)/.3f));
            finisherDuckGain=Mathf.Lerp(Mathf.Pow(10,-Mathf.Max(0,finisherDuckDb)/20),1,recovery);
        }
        private void PlayFinisherNotification(bool warning) {
            float gain=warning ? finisherWarningVolume : finisherReadyVolume;
            if(paused || gain<=0 || masterVolume<=0)return;
            // Listener-relative tactical cues stay clear even when the enemy is far ahead.
            if(Play(warning ? finisherWarning : finisherReady,Vector3.zero,gain,1,true,priority:24)<0)return;
            if(warning)FinisherWarningCueCount++;else FinisherReadyCueCount++;
            finisherDuckAge=0;UpdateFinisherDucking(0);ApplyRidingLevels();
        }
        public void ForgetCar(Car car){
            relativePositions.Remove(car);
            // Match a destroyed anchor: let the recorded tail finish in place.
            for(int i=0;i<voiceAnchors.Length;i++)if(voiceAnchors[i]!=null && voiceAnchors[i].IsChildOf(car.transform)){voices[i].transform.position=voiceAnchors[i].position;voiceAnchors[i]=null;}
        }
        private void ScanTraffic() {
            if (player == null) return;
            float scanDelta = lastTrafficScan >= 0 ? Time.time - lastTrafficScan : 0;
            lastTrafficScan = Time.time;
            Car closestWhoosh = null;
            float closestLateral = float.MaxValue;
            float closestSpeed = 0;
            float closestSide = 1;
            var cars = Car.Instances;
            for (int carIndex=0;carIndex<cars.Count;carIndex++) {
                var car=cars[carIndex];
                if(car==null || !car.gameObject.activeInHierarchy)continue;
                if (!car.IsAlive) continue;
                Vector3 relative = car.transform.position - player.transform.position;
                if (relativePositions.TryGetValue(car, out Vector3 previous)) {
                    bool crossed = (previous.z >= 0 && relative.z < 0) || (previous.z <= 0 && relative.z > 0);
                    float relativeSpeed = scanDelta > 0.0001f ? Mathf.Abs(relative.z - previous.z) / scanDelta : 0;
                    if (crossed && relativeSpeed >= minimumRelativeSpeed) {
                        // A fast rider can move sideways several units between scans; evaluate
                        // proximity at the crossing, not at the end of the scan.
                        float crossingFraction = Mathf.Clamp01(previous.z / (previous.z - relative.z));
                        float crossingX = Mathf.Lerp(previous.x, relative.x, crossingFraction);
                        float lateral = Mathf.Abs(crossingX);
                        if (lateral <= passByDistance && Time.time >= nextPass) {
                            if (trafficVolume > 0) Play(Pick(passBys), car.transform.position,
                                trafficVolume * Mathf.Lerp(1, 0.25f, lateral / passByDistance),
                                1f, false, car.transform);
                            PassByCount++; nextPass = Time.time + 0.12f;
                        }
                        // Car enumeration order is unspecified. Pick the nearest eligible pass
                        // independently of the traffic-sound cooldown, including when traffic is muted.
                        if (lateral < whooshDistance && lateral < closestLateral) {
                            closestWhoosh = car;
                            closestLateral = lateral;
                            closestSpeed = relativeSpeed;
                            closestSide = crossingX >= 0 ? 1 : -1;
                        }
                    }
                }
                relativePositions[car] = relative;
            }
            if (closestWhoosh != null && Time.time >= nextWhoosh) {
                float proximity = Mathf.Clamp01(closestLateral / Mathf.Max(0.1f, whooshDistance));
                float speedGain = Mathf.InverseLerp(minimumRelativeSpeed, Mathf.Max(minimumRelativeSpeed + 1, referenceSpeed), closestSpeed);
                if (passWhoosh != null && passWhooshVolume > 0) {
                    float gain = passWhooshVolume * Mathf.Lerp(1f, 0.65f, proximity) * Mathf.Lerp(0.8f, 1f, speedGain);
                    Play(passWhoosh, closestWhoosh.transform.position, gain, 1f, false, closestWhoosh.transform, whoosh: true);
                    WhooshCount++;
                }
                if (passCamera == null && Camera.main != null) passCamera = Camera.main.GetComponent<CameraController>();
                if (passCamera != null) passCamera.TriggerPassShake(Mathf.Lerp(1f, 0.25f, proximity) * Mathf.Lerp(0.35f, 1f, speedGain), -closestSide);
                nextWhoosh = Time.time + Mathf.Max(0, whooshInterval);
            }
            removed.Clear();
            foreach (var pair in relativePositions) if (pair.Key == null || !pair.Key.IsAlive || Mathf.Abs(pair.Value.z) > 150) removed.Add(pair.Key);
            foreach (var car in removed) relativePositions.Remove(car);
        }
        private void BindJacket() {
            // Use the animated centre hem, rather than the bike root or listener.
            Transform garment = null;
            foreach (var bone in player.GetComponentsInChildren<Transform>(true)) {
                if (bone.name == "Jacket") garment = bone;
                if (bone.name == "jacket_01_03") { jacketAnchor = bone; break; }
            }
            if (jacketAnchor == null) jacketAnchor = garment;
            if (jacketAnchor == null || jacketSource == null) return;
            jacketSource.transform.SetParent(jacketAnchor, false);
            jacketSource.transform.localPosition = Vector3.zero;
            previousJacketRotation = jacketAnchor.localRotation;
        }
        private void LateUpdate() {
            if (jacketSource == null || jacketAnchor == null || paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            float angularSpeed = Quaternion.Angle(previousJacketRotation, jacketAnchor.localRotation) / dt;
            previousJacketRotation = jacketAnchor.localRotation;
            jacketFlutter = Mathf.Lerp(jacketFlutter, Mathf.Clamp01(angularSpeed / Mathf.Max(1, jacketMotionReference)), 1 - Mathf.Exp(-dt * 8));
            float speed = bike != null ? bike.linearVelocity.magnitude : 0;
            float pressure = Mathf.Pow(Mathf.Clamp01(speed / Mathf.Max(1, referenceSpeed)), 1.2f);
            float lean = player != null ? Mathf.Abs(player.xThrow) : 0;
            bool riding = player != null && player.IsAlive && manager != null && manager.gameState == GameState.Playing;
            float intensity = Mathf.Clamp01(0.65f + jacketFlutter * 0.35f + lean * jacketSteeringBoost);
            float gain = riding ? masterVolume * jacketVolume * pressure * intensity : 0;
            jacketSource.volume = Mathf.Lerp(jacketSource.volume, gain, 1 - Mathf.Exp(-dt * 8));
            jacketSource.pitch = Mathf.Lerp(0.9f, 1.25f, pressure) + jacketFlutter * 0.05f;
        }
        private void OnHealthChanged(float current, float maximum) {
            if (current < previousHealth && player != null) OnSound(new SoundEvent(SoundCue.Impact, player.transform.position));
            previousHealth = current;
        }
        private void OnStateChanged(GameState previous, GameState current) {
            SetEnemyArrivalPaused(current!=GameState.Playing);
            if (current == GameState.Pause) { SetPaused(true); Play(pause, Vector3.zero, interfaceVolume, 1, true, menu: true); }
            else {
                if (paused) { SetPaused(false); Play(resume, Vector3.zero, interfaceVolume, 1, true, menu: true); }
                if (current == GameState.GameOver) Play(gameOver, Vector3.zero, interfaceVolume, 1, true, menu: true);
                else if (current == GameState.Hit && player != null) OnSound(new SoundEvent(SoundCue.Crash, player.transform.position));
            }
        }
        private void SetPaused(bool value) {
            paused = value;
            ConfigureEngineReverb(engineReverb);
            ConfigureEngineReverb(launchReverb);
            ConfigureEngineReverb(launchPowerReverb);
            if(launchPowerSource!=null){if(value)launchPowerSource.Pause();else launchPowerSource.UnPause();}
            if (value) { relativePositions.Clear(); lastTrafficScan = -1; }
            if (value) { engineSource.Pause(); launchSource.Pause(); windSource.Pause(); windRushSource.Pause(); }
            else { engineSource.UnPause(); launchSource.UnPause(); windSource.UnPause(); windRushSource.UnPause(); }
            if (jacketSource != null) {
                if (value) jacketSource.Pause(); else jacketSource.UnPause();
            }
            if (jacketAnchor != null) previousJacketRotation = jacketAnchor.localRotation;
            for (int i = 0; i < voices.Length; i++) {
                if (value && voices[i].isPlaying && !voices[i].ignoreListenerPause) { voices[i].Pause(); suspended[i] = true; }
                else if (!value && suspended[i]) { voices[i].UnPause(); suspended[i] = false; }
                if (whooshVoices[i]) ConfigureWhooshReverb(i);
            }
        }
        private void OnSound(SoundEvent sound) {
            switch (sound.Cue) {
                case SoundCue.PlayerShot:
                    var clip=Pick(shots);
                    if(clip==null || voices==null || paused || weaponVolume<=0 || masterVolume<=0)break;
                    pistolDuckElapsed=0;pistolDuckActive=true;UpdatePistolDucking(0);ApplyRidingLevels();
                    Play(clip,sound.Position,weaponVolume,Range(0.985f,1.015f),true,priority:48);
                    break;
                case SoundCue.EnemyShot: Play(Pick(shots), sound.Position, weaponVolume * 0.65f, Range(0.985f, 1.015f), false); break;
                case SoundCue.Explosion: Play(Pick(explosions), sound.Position, explosionVolume, Range(0.94f, 1.03f), false); break;
                case SoundCue.Pickup: Play(pickup, sound.Position, interfaceVolume, 1, true); break;
                case SoundCue.UiClick: Play(uiClick, Vector3.zero, interfaceVolume, 1, true, menu: true); break;
                case SoundCue.EnemyFinisherReady: PlayFinisherNotification(false); break;
                case SoundCue.EnemyFinisherWarning: PlayFinisherNotification(true); break;
                case SoundCue.PistolHitConfirm:
                case SoundCue.PlayerHurt:
                case SoundCue.BulletNearMiss: PlayGunFeedback(sound);break;
                default:
                    if (Time.time < nextImpact) return;
                    nextImpact = Time.time + 0.08f;
                    Play(Pick(impacts), sound.Position, impactVolume, Range(0.92f, 1.07f), sound.Cue == SoundCue.Crash);
                    break;
            }
        }
        private float Range(float minimum, float maximum) => minimum + (float)variation.NextDouble() * (maximum - minimum);
        private AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[variation.Next(clips.Length)] : null;
        private int Play(AudioClip clip, Vector3 position, float gain, float pitch, bool local, Transform anchor = null, bool menu = false, bool whoosh = false, int priority = 80) {
            if (clip == null || voices == null || (paused && !menu)) return -1;
            int index = nextVoice;
            for (int i = 0; i < voices.Length; i++) {
                int candidate = (nextVoice + i) % voices.Length;
                if (!voices[candidate].isPlaying && !suspended[candidate]) { index = candidate; break; }
            }
            nextVoice = (index + 1) % voices.Length;
            var source = voices[index]; source.Stop(); suspended[index] = false;
            source.panStereo=0;
            source.priority=priority;
            source.transform.position = position; source.spatialBlend = local ? 0 : 0.85f;
            // A car can move a long way during its audible tail at arcade speeds.
            // Restore the ordinary range when this voice is reused for another effect.
            source.minDistance = whoosh ? 12 : 5;
            source.maxDistance = whoosh ? 160 : 65;
            whooshVoices[index] = whoosh;
            if(enemyArrivalWhooshVoices!=null)enemyArrivalWhooshVoices[index]=false;
            if (whoosh && whooshReverbs[index] == null) {
                var filter = source.gameObject.AddComponent<AudioReverbFilter>();
                filter.reverbPreset = AudioReverbPreset.User;
                filter.dryLevel = 0; filter.room = -350; filter.roomHF = -1800;
                filter.decayHFRatio = 0.55f;
                filter.reflectionsDelay = 0.012f; filter.reverbDelay = 0.025f;
                whooshReverbs[index] = filter;
            }
            ConfigureWhooshReverb(index);
            source.ignoreListenerPause = menu; source.pitch = pitch; source.clip = clip;
            voiceAnchors[index] = anchor; voiceGains[index] = gain;
            source.volume = masterVolume * gain; source.Play(); PlayedCueCount++;
            return index;
        }
        private void OnDestroy() {
            // This source is parented to the animated jacket, outside the audio rig.
            if (jacketSource != null) Destroy(jacketSource.gameObject);
            if (boundSession && manager != null && manager.Session != null) manager.Session.StateChanged -= OnStateChanged;
            if (boundHealth != null) boundHealth.Changed -= OnHealthChanged;
            if (Active == this) Active = null;
        }
    }
}
