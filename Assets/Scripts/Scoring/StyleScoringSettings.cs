using UnityEngine;

namespace CyberBiker.Scoring {
    public enum StyleAction { PistolHit, PistolKill, SwordFinisher, BulletDodge, PerfectDodge, NearMiss, GapPass, SpeedRun, HardCarve }
    [System.Serializable]
    public sealed class StyleScoringSettings {
        public bool enabled=true;
        [Range(0,1)] public float distancePointsPerUnit=.01f;
        [Header("Combo rules")]
        [Range(.5f,30)] public float comboWindow=5;
        [Range(1,20)] public int maximumMultiplier=10;
        [Min(1)] public int stepsPerMultiplier=4;
        [Tooltip("Repeated moves lose value. Mixing moves gives two multiplier steps instead of one.")]
        [Range(.1f,1)] public float repetitionValue=.72f;
        [Range(0,1)] public float minimumRepeatValue=.25f;
        [Range(0,100)] public int minimumAwardPoints=1;
        public bool holdComboDuringFinisher=true,alternateCarveSides=true;
        [Range(0,10)] public int varietySteps=2,repeatSteps=1;
        [Range(0,1)] public float impactPayoutFraction=.5f;
        [Header("Points per move")]
        [Min(0)] public int pistolHit=40;
        [Min(0)] public int pistolKill=350;
        [Min(0)] public int swordFinisher=800;
        [Min(0)] public int bulletDodge=150;
        [Min(0)] public int perfectDodge=350;
        [Min(0)] public int nearMiss=120;
        [Min(0)] public int gapPass=300;
        [Min(0)] public int speedRun=100;
        [Min(0)] public int hardCarve=75;
        [Header("Driving detection")]
        [Range(.1f,6)] public float nearMissClearance=1.2f;
        [Min(0)] public float minimumDrivingSpeed=25;
        [Tooltip("Clean driving interval for the speed bonus. Prevent Passive Speed Chains can require a longer interval than the combo window.")]
        [Range(.5f,60)] public float cleanSpeedInterval=8;
        [Range(.1f,1)] public float carveSteering=.75f;
        [Range(.05f,3)] public float carveHold=.45f;
        [Range(0,1)] public float minimumNearMissClearance=.03f;
        [Range(0,3)] public float razorClearance=.35f;
        [Range(1,10)] public float razorMultiplier=2;
        [Range(0,10)] public float carveCooldown=2,postHitDrivingCooldown=2;
        [Range(.1f,1)] public float speedThreshold=.9f;
        public bool preventPassiveSpeedChains=true;
        [Range(.1f,10)] public float passiveSpeedGap=1;
        [Header("Presentation")]
        public TMPro.TMP_FontAsset displayFont;
        [Tooltip("Typeface for floating +numbers, separate from the main score HUD.")]
        public Font popupTypeface;
        public bool showHud=true;
        public bool worldPopups=true;
        [Header("3D score numbers")]
        [Range(.3f,2)] public float worldNumberLifetime=.9f;
        [Range(.005f,.04f)] public float worldNumberScale=.018f;
        [Tooltip("Settled brightness after the brief appearance glow.")]
        [Range(.1f,1)] public float worldNumberBrightness=.82f;
        [Tooltip("Initial brightness settles smoothly; reduced motion uses the settled value throughout.")]
        [Range(.1f,1)] public float worldNumberAppearanceBrightness=.94f;
        [Range(.05f,.5f)] public float worldNumberGlowSettle=.22f;
        [Range(0,1)] public float worldNumberBackgroundOpacity=.96f;
        [Tooltip("Shrink close numbers to keep them from filling the view. Farther numbers retain world-space perspective.")]
        [Range(6,30)] public float worldNumberNearDistance=16;
        [Range(0,1.5f)] public float worldNumberRise=.45f;
        public Vector3 worldNumberOffset=new Vector3(0,.9f,0);
        [Tooltip("Keeps score feedback readable without bouncing, rotating, flying numbers or confetti.")]
        public bool reducedMotion;
        [Range(0,2)] public float animationIntensity=1;
        [Range(0,1)] public float soundVolume=.75f;
        [Tooltip("Safe-area normalized position; (0.5, 0.82) is above the bike and below the top HUD.")]
        public Vector2 hudPosition=new Vector2(.5f,.82f);
        [Range(.65f,1.4f)] public float hudScale=1;
        [Header("Score board motion")]
        [Range(0,.4f)] public float boardScorePunch=.18f;
        [Range(0,.5f)] public float boardMultiplierPunch=.32f;
        [Range(0,.4f)] public float boardSweepStrength=.19f;
        [Range(.15f,.7f)] public float boardBankCountSeconds=.32f;
        [Range(0,1)] public float boardComboEnergy=.75f;
        public int Points(StyleAction action) {
            switch(action) {
                case StyleAction.PistolHit:return pistolHit;case StyleAction.PistolKill:return pistolKill;
                case StyleAction.SwordFinisher:return swordFinisher;case StyleAction.BulletDodge:return bulletDodge;
                case StyleAction.PerfectDodge:return perfectDodge;case StyleAction.NearMiss:return nearMiss;
                case StyleAction.GapPass:return gapPass;case StyleAction.SpeedRun:return speedRun;default:return hardCarve;
            }
        }
        public static string Label(StyleAction action) {
            switch(action) {
                case StyleAction.PistolHit:return "ON TARGET";case StyleAction.PistolKill:return "TAKEDOWN";
                case StyleAction.SwordFinisher:return "SWORD FINISHER";case StyleAction.BulletDodge:return "BULLET DODGE";
                case StyleAction.PerfectDodge:return "PERFECT DODGE";case StyleAction.NearMiss:return "CLOSE SHAVE";
                case StyleAction.GapPass:return "THREAD THE NEEDLE";case StyleAction.SpeedRun:return "FULL THROTTLE";default:return "HARD CARVE";
            }
        }
    }
}
