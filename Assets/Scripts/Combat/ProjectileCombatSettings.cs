using UnityEngine;

namespace CyberBiker.Combat {
    [System.Serializable]
    public sealed class ProjectileCombatSettings {
        [Header("Enemy bullet trajectory experiment")]
        [Tooltip("Replaces permanent gun lasers with a green trajectory visible only while each bullet is in flight.")]
        public bool enemyTrajectoryGuides = true;
        [Range(.01f,.12f)] public float enemyTrajectoryWidth = .025f;
        [Range(.1f,1.25f)] public float enemyTrajectoryBrightness = .8f;
        [Range(.1f,1)] public float enemyTrajectoryOpacity = .85f;
        [Range(0,.75f)] public float enemyTrajectoryEndFade = .25f;
        public Color enemyTrajectorySafeColor = new Color(.08f,1,.2f,1);
        [Header("Helmet danger sense")]
        public bool helmetThreatSense = true;
        public Color helmetSenseColor = new Color(.12f,1,.25f,1);
        [Range(.25f,1)] public float helmetSenseRadius = .48f;
        [Range(.1f,.8f)] public float helmetSenseLength = .46f;
        [Range(.015f,.12f)] public float helmetSenseWidth = .07f;
        [Range(.1f,3)] public float helmetSenseBrightness = 1.6f;
        [Tooltip("Pulse cycles per real second for a distant incoming bullet.")]
        [Range(.5f,4)] public float helmetSenseFarPulse = 1.8f;
        [Tooltip("Pulse cycles per real second just before a bullet reaches the player.")]
        [Range(4,10)] public float helmetSenseNearPulse = 6.5f;
        [Tooltip("Distance in world units over which the warning accelerates toward its fastest pulse.")]
        [Range(5,80)] public float helmetSenseUrgencyDistance = 30;
        [Range(-.2f,.6f)] public float helmetSenseHeight = .2f;
        [Range(.02f,.25f)] public float helmetSenseFadeIn = .05f;
        [Range(.03f,.5f)] public float helmetSenseFadeOut = .14f;
        [Tooltip("Padding around the animated rider and solid bike bounds for the helmet warning only. Does not change bullet collisions.")]
        [Range(0,.6f)] public float helmetSensePadding = .18f;
        [Header("Enemy shot warning")]
        [Tooltip("Full gameplay seconds of red warning before each shot. The enemy firing cadence expands when needed to preserve this window.")]
        [Range(0,1.5f)] public float firingWarning = .6f;
        [Range(0,.8f)] public float enemyShotRecovery = .2f;
        public bool enemyAimLaser = true;
        [Range(.01f,.12f)] public float enemyAimLaserWidth = .03f;
        [Range(.15f,.8f)] public float enemyAimLaserWhiteWidthMultiplier = .45f;
        [Range(.1f,1.25f)] public float enemyAimLaserBrightness = .8f;
        public Color enemyAimLaserIdleColor = Color.white;
        public Color enemyAimLaserWarningColor = new Color(1,.015f,.025f,1);
        [Header("Enemy laser fade and shot response")]
        [Tooltip("Fraction of the full enemy-to-player beam that fades out near the player. 0.25 fades only the last quarter; zero disables it.")]
        [Range(0,.75f)] public float enemyAimLaserPlayerFadeFraction = .25f;
        [Tooltip("Gameplay seconds to keep the firing gun's laser dim after a real shot.")]
        [Range(0,.4f)] public float enemyAimLaserShotDimDuration = .12f;
        [Range(.01f,.4f)] public float enemyAimLaserShotRestore = .1f;
        [Range(0,1)] public float enemyAimLaserShotDimFactor = .15f;
        [Header("Enemy muzzle flash and smoke")]
        [Range(.03f,.3f)] public float enemyFlashScale = .11f;
        [Range(.1f,1)] public float enemyFlashBrightness = .85f;
        [Range(.025f,.2f)] public float enemyFlashDuration = .095f;
        [Range(0,3)] public float enemyFlashLight = .8f;
        [Range(0,12)] public int enemySmokeCount = 7;
        [Range(.15f,1)] public float enemySmokeLifetime = .55f;
        [Range(0,60)] public float smokeWindSweep = 28;
        [Header("Player pistol muzzle flash")]
        [Range(.03f,.3f)] public float playerFlashScale = .12f;
        [Range(.1f,1)] public float playerFlashBrightness = .95f;
        [Range(.025f,.2f)] public float playerFlashDuration = .075f;
        [Range(0,3)] public float playerFlashLight = 1.1f;
        [Header("Bullet air wake (original projectile retained)")]
        public bool bulletAirWake = true;
        [Range(.5f,6)] public float airWakeLength = 3.2f;
        [Range(.015f,.16f)] public float airWakeWidth = .065f;
        [Range(.1f,.8f)] public float airWakeOpacity = .42f;
        [Header("Evade timing (real seconds)")]
        public bool timedEvade = true;
        [Range(.4f,2)] public float warningHorizon = 1.1f;
        [Range(.1f,.6f)] public float evadeWindow = .3f;
        [Range(.04f,.25f)] public float perfectWindow = .12f;
        [Range(.15f,1)] public float recovery = .42f;
        public KeyCode evadeKey = KeyCode.E;
        [Header("Rider movement")]
        [Range(5,35)] public float duckDegrees = 22;
        [Range(5,35)] public float twistDegrees = 18;
        [Range(.05f,.5f)] public float bodyShift = .25f;
        [Range(0,2)] public float cameraRoll = .65f;
        [Range(0,3)] public float perfectFovKick = .8f;
        [Header("Perfect dodge reward")]
        public bool perfectSlowMotion = true;
        [Range(.3f,1)] public float perfectTimeScale = .6f;
        [Range(.05f,.5f)] public float perfectSlowDuration = .24f;
        [Range(.05f,.5f)] public float slowRecovery = .18f;
        [Range(0,5)] public float rewardCooldown = 1.2f;
        [Range(0,.3f)] public float steeringDodgeGrace = .05f;
        [Header("Optional incoming-hit assist")]
        public bool incomingSlowAssist = false;
        [Range(.1f,.5f)] public float assistWindow = .22f;
        [Range(.4f,1)] public float assistTimeScale = .7f;
        public float EvadeWindow => Mathf.Clamp(evadeWindow,.05f,.8f);
        public float PerfectWindow => Mathf.Clamp(perfectWindow,.01f,EvadeWindow);
    }
}
