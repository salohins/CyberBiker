using UnityEngine;

public class Difficulty : MonoBehaviour
{
    [Header("Traffic")]
    public float carOffsetModifier = 0;
    public int oneCarRow = 1;
    public int twoCarRow = 1;
    public int threeCarRow = 1;

    [Header("Enemy encounters")]
    [Tooltip("Total riders in each wave, fought one at a time. Zero stops new enemy spawns; existing riders finish their encounter.")]
    [Range(0,10)] public int enemiesPerWave = 3;
    [Tooltip("Maximum live riders, including the fighter and waiting riders. A larger wave refills the queue as riders are defeated.")]
    [Range(1,10)] public int maxEnemiesOnRoad = 3;
    [Tooltip("Gameplay seconds before the first rider appears when the scene has no existing enemy.")]
    [Min(0)] public float enemyInitialDelay = 2;
    [Tooltip("Gameplay seconds between adding riders to the current wave. Zero fills the queue over consecutive frames.")]
    [Min(0)] public float enemySpawnInterval = .5f;
    [Tooltip("Gameplay seconds after the entire wave is defeated before starting the next wave. Zero allows back-to-back waves.")]
    [Min(0)] public float enemyWaveCooldown = 5;
    [Tooltip("Gameplay seconds after a fighter is defeated before the first waiting rider takes over. Zero promotes it immediately.")]
    [Min(0)] public float enemyHandoffDelay = .2f;
    [Header("Enemy arrivals")]
    [Tooltip("Percentage of riders catching up from behind and overtaking. The others approach from farther ahead.")]
    [Range(0,100)] public float enemyRearArrivalPercent = 60;
    [InspectorName("Front Arrival Distance")]
    [Tooltip("Minimum forward introduction distance. Front arrivals fade in at a distance and approach their slot before fighting.")]
    [Min(100)] public float enemySpawnDistance = 180;
    [Tooltip("Rear introduction distance. Spawning waits for a clear lane outside the gameplay camera view.")]
    [Min(20)] public float enemyRearSpawnDistance = 45;
    [Tooltip("Maximum speed difference from the player while arriving, in world units per second.")]
    [Range(10,120)] public float enemyArrivalClosingSpeed = 40;
    [Tooltip("Gameplay seconds to become fully visible at the start of an arrival. Materials return to their original appearance afterward.")]
    [Range(.2f,3)] public float enemyArrivalFadeTime = .8f;
    [Tooltip("Extra forward distance between waiting riders. Their first slot is this far beyond GameplayManager's fighting distance.")]
    [Min(8)] public float enemyQueueSpacing = 18;

    [Header("Enemy riding style")]
    [Tooltip("Gameplay seconds between attempts to change preferred lane. Riders swap lanes or use a less crowded one to keep the group spread out.")]
    [Range(1,8)] public float enemyLaneChangeInterval = 2.8f;
    [InspectorName("Slide Lean Reference Speed")]
    [Tooltip("Actual sideways speed at which visual drift and lean reach full strength. Does not limit pathfinding movement.")]
    [Range(3,16)] public float enemyLateralSpeed = 9;
    [Tooltip("Small continuous movement inside a lane; large changes still follow clear traffic routes.")]
    [Range(0,1.5f)] public float enemyLaneWeave = .45f;
    [Tooltip("Visual sideways yaw during a slide; does not change the collision direction or move the enemy to a finisher's side.")]
    [Range(0,20)] public float enemyDriftAngle = 11;
    [Range(10,45)] public float enemyRidingLean = 34;
}
