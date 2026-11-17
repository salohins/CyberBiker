using System;
using CyberBiker.Balance;
using CyberBiker.Player;
using Gameplay;
using UnityEngine;

namespace CyberBiker.UI {
    [Serializable]
    public sealed class TestingSettings {
        public const string PreferenceKey = "CyberBiker.TestSettings.v1";
        public float maxSpeed, acceleration, steeringAcceleration, steeringSpeed;
        public float launchMultiplier, launchDuration, pistolDamage, shotInterval, enemyDistance;
        public float launchRefillScore=1500,launchMinimumCharge=10,launchStartingCharge=100;
        public float touchSensitivity;
        public MobileSteeringMode steeringMode=MobileSteeringMode.Swipe;
        public float gyroSensitivity=1,gyroDeadZone=2,gyroSmoothing=.12f;
        public bool gyroInvert;
        public float scoreAnimationIntensity=1,scoreSoundVolume=.75f;
        public ScoreBalanceTuning scoreBalance=new ScoreBalanceTuning();
        public TriggerBalanceTuning triggerBalance=new TriggerBalanceTuning();
        public static TestingSettings Defaults { get; private set; }

        public TestingSettings Copy(){var copy=(TestingSettings)MemberwiseClone();copy.scoreBalance=scoreBalance?.Copy() ?? new ScoreBalanceTuning();copy.triggerBalance=triggerBalance?.Copy() ?? new TriggerBalanceTuning();return copy;}
        public static TestingSettings Read(PlayerController player, GameplayManager gameplay, BikeSwordCombat sword) => new TestingSettings {
            maxSpeed = player.ConfiguredMaxSpeed,
            acceleration = player.ForwardAcceleration, steeringAcceleration = player.SteeringAcceleration,
            steeringSpeed = player.SteeringSpeedLimit,
            launchMultiplier = sword != null ? sword.TestingSpeedMultiplier : 1.35f,
            launchDuration = sword != null ? sword.TestingLaunchDuration : 7,
            launchRefillScore=sword!=null ? sword.TestingLaunchRefillScore : 1500,
            launchMinimumCharge=sword!=null ? sword.TestingLaunchMinimumCharge : 10,
            launchStartingCharge=sword!=null ? sword.TestingLaunchStartingCharge : 100,
            pistolDamage = gameplay.PlayerPistolDamage, shotInterval = gameplay.PlayerPistolShotInterval,
            enemyDistance = gameplay.EnemyDistanceFromPlayer,
            touchSensitivity = player.GetComponent<TouchInputManager>().TouchSteeringSensitivity,
            steeringMode = player.GetComponent<TouchInputManager>().SteeringMode,
            gyroSensitivity = player.GetComponent<TouchInputManager>().GyroSensitivity,
            gyroDeadZone = player.GetComponent<TouchInputManager>().GyroDeadZone,
            gyroSmoothing = player.GetComponent<TouchInputManager>().GyroSmoothing,
            scoreAnimationIntensity=gameplay.StyleScoring.animationIntensity,
            scoreSoundVolume=gameplay.StyleScoring.soundVolume,
            scoreBalance=ScoreBalanceTuning.Read(gameplay.StyleScoring),triggerBalance=TriggerBalanceTuning.Read(player,gameplay,sword),
            gyroInvert = player.GetComponent<TouchInputManager>().GyroInverted
        };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { Defaults = null; }
        public static void Initialize(PlayerController player, GameplayManager gameplay, BikeSwordCombat sword) {
            Defaults = Read(player, gameplay, sword);
            if (!PlayerPrefs.HasKey(PreferenceKey)) return;
            try {
                // Overlay saved fields on the current Inspector defaults.
                var saved = Defaults.Copy();
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PreferenceKey), saved);
                saved.Apply(player, gameplay, sword);
            } catch (ArgumentException) { Debug.LogWarning("Ignoring invalid saved test settings."); }
        }
        private static float Safe(float value, float fallback, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public void Normalize() {
            if(scoreBalance==null)scoreBalance=new ScoreBalanceTuning();if(triggerBalance==null)triggerBalance=new TriggerBalanceTuning();scoreBalance.Normalize();triggerBalance.Normalize();
            scoreAnimationIntensity=Safe(scoreAnimationIntensity,1,0,2);
            scoreSoundVolume=Safe(scoreSoundVolume,.75f,0,1);
            maxSpeed = Safe(maxSpeed, 75, 1, 200);
            acceleration = Safe(acceleration, 300, 0, 1000);
            steeringAcceleration = Safe(steeringAcceleration, 50, 0, 200);
            steeringSpeed = Safe(steeringSpeed, 20, 0, 60);
            launchMultiplier = Safe(launchMultiplier, 1.35f, 1, 2);
            launchDuration = Safe(launchDuration, 7, 1, 30);
            launchRefillScore=Safe(launchRefillScore,1500,1,100000);
            launchMinimumCharge=Safe(launchMinimumCharge,10,1,100);
            launchStartingCharge=Safe(launchStartingCharge,100,0,100);
            pistolDamage = Safe(pistolDamage, 15, 0, 200);
            shotInterval = Safe(shotInterval, .45f, .05f, 3);
            enemyDistance = Safe(enemyDistance, 20, 1, 150);
            touchSensitivity = Safe(touchSensitivity, 10, 0, 19);
            if(steeringMode!=MobileSteeringMode.Swipe && steeringMode!=MobileSteeringMode.Gyroscope)steeringMode=MobileSteeringMode.Swipe;
            gyroSensitivity=Safe(gyroSensitivity,1,.25f,3);gyroDeadZone=Safe(gyroDeadZone,2,0,5);gyroSmoothing=Safe(gyroSmoothing,.12f,0,.4f);
        }
        public void Apply(PlayerController player, GameplayManager gameplay, BikeSwordCombat sword) {
            Normalize();
            player.ConfigureTestingMovement(maxSpeed, acceleration, steeringAcceleration, steeringSpeed);
            gameplay.PlayerPistolDamage = pistolDamage;
            gameplay.PlayerPistolShotInterval = shotInterval;
            gameplay.EnemyDistanceFromPlayer = enemyDistance;
            gameplay.StyleScoring.animationIntensity=scoreAnimationIntensity;
            gameplay.StyleScoring.soundVolume=scoreSoundVolume;
            scoreBalance.Apply(gameplay.StyleScoring);triggerBalance.Apply(player,gameplay,sword);gameplay.StyleScore?.RefreshBalance();
            player.GetComponent<TouchInputManager>().TouchSteeringSensitivity = touchSensitivity;
            player.GetComponent<TouchInputManager>().ConfigureGyroscope(steeringMode,gyroSensitivity,gyroDeadZone,gyroSmoothing,gyroInvert);
            if (sword != null) { sword.TestingSpeedMultiplier = launchMultiplier; sword.TestingLaunchDuration = launchDuration; sword.TestingLaunchRefillScore=launchRefillScore;sword.TestingLaunchMinimumCharge=launchMinimumCharge;sword.TestingLaunchStartingCharge=launchStartingCharge; }
        }
        public void Save() { Normalize(); PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static void ClearSaved() { PlayerPrefs.DeleteKey(PreferenceKey); PlayerPrefs.Save(); }
    }
}
