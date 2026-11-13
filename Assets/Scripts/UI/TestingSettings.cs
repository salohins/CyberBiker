using System;
using CyberBiker.Player;
using Gameplay;
using UnityEngine;

namespace CyberBiker.UI {
    [Serializable]
    public sealed class TestingSettings {
        public const string PreferenceKey = "CyberBiker.TestSettings.v1";
        public float maxSpeed, acceleration, steeringAcceleration, steeringSpeed;
        public float launchMultiplier, launchDuration, pistolDamage, shotInterval, enemyDistance;
        public float touchSensitivity;
        public static TestingSettings Defaults { get; private set; }

        public TestingSettings Copy() => (TestingSettings)MemberwiseClone();
        public static TestingSettings Read(PlayerController player, GameplayManager gameplay, BikeSwordCombat sword) => new TestingSettings {
            maxSpeed = player.ConfiguredMaxSpeed,
            acceleration = player.ForwardAcceleration, steeringAcceleration = player.SteeringAcceleration,
            steeringSpeed = player.SteeringSpeedLimit,
            launchMultiplier = sword != null ? sword.TestingSpeedMultiplier : 1.35f,
            launchDuration = sword != null ? sword.TestingLaunchDuration : 7,
            pistolDamage = gameplay.PlayerPistolDamage, shotInterval = gameplay.PlayerPistolShotInterval,
            enemyDistance = gameplay.EnemyDistanceFromPlayer,
            touchSensitivity = player.GetComponent<TouchInputManager>().TouchSteeringSensitivity
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
            maxSpeed = Safe(maxSpeed, 75, 1, 200);
            acceleration = Safe(acceleration, 300, 0, 1000);
            steeringAcceleration = Safe(steeringAcceleration, 50, 0, 200);
            steeringSpeed = Safe(steeringSpeed, 20, 0, 60);
            launchMultiplier = Safe(launchMultiplier, 1.35f, 1, 2);
            launchDuration = Safe(launchDuration, 7, 1, 30);
            pistolDamage = Safe(pistolDamage, 15, 0, 200);
            shotInterval = Safe(shotInterval, .45f, .05f, 3);
            enemyDistance = Safe(enemyDistance, 20, 1, 150);
            touchSensitivity = Safe(touchSensitivity, 10, 0, 19);
        }
        public void Apply(PlayerController player, GameplayManager gameplay, BikeSwordCombat sword) {
            Normalize();
            player.ConfigureTestingMovement(maxSpeed, acceleration, steeringAcceleration, steeringSpeed);
            gameplay.PlayerPistolDamage = pistolDamage;
            gameplay.PlayerPistolShotInterval = shotInterval;
            gameplay.EnemyDistanceFromPlayer = enemyDistance;
            player.GetComponent<TouchInputManager>().TouchSteeringSensitivity = touchSensitivity;
            if (sword != null) { sword.TestingSpeedMultiplier = launchMultiplier; sword.TestingLaunchDuration = launchDuration; }
        }
        public void Save() { Normalize(); PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static void ClearSaved() { PlayerPrefs.DeleteKey(PreferenceKey); PlayerPrefs.Save(); }
    }
}
