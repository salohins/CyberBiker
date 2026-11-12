using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AI;

public sealed class TrafficSpawner : MonoBehaviour {
    private GameObject[] cars;
    private GameObject enemy, activeEnemy, player;
    private AIController aiController;
    private DifficultyManager difficultyManager;
    private int carBufferSize;
    private Vector3 carSpawnPosition, _playerPosition;
    private bool initialized;
    private bool hasSpawned;
    private readonly List<GameObject> activeCars = new List<GameObject>();
    private readonly List<List<DodgePoint>> rowsOfCars = new List<List<DodgePoint>>();
    public void Initialize(GameObject[] cars, GameObject enemy, GameObject player, AIController aiController,
        DifficultyManager difficultyManager, float startDistance, int bufferSize) {
        this.cars = cars; this.enemy = enemy; this.player = player;
        this.aiController = aiController; this.difficultyManager = difficultyManager;
        carBufferSize = Mathf.Max(1, bufferSize);
        var existing = FindAnyObjectByType<Enemy>();
        activeEnemy = existing != null ? existing.gameObject : null;
        carSpawnPosition = player.transform.position + Vector3.forward * startDistance + Vector3.up;
        initialized = cars != null && cars.Length > 0 && aiController != null && difficultyManager != null;
    }
    private void Update() {
        if (!initialized) return;
        _playerPosition = player.transform.position;
        aiController.dodgePoints.RemoveAll(point => point.offsetObject == null || point.GetPosition().z < _playerPosition.z);
        for (int i = activeCars.Count - 1; i >= 0; i--) {
            var car = activeCars[i];
            if (car == null) { activeCars.RemoveAt(i); continue; }
            if (car.transform.position.z < _playerPosition.z - 20f) {
                Destroy(car); activeCars.RemoveAt(i);
            }
        }
        rowsOfCars.RemoveAll(row => row.Count == 0 || row.Max(point => point.GetPosition().z) < _playerPosition.z);
        if (hasSpawned && rowsOfCars.Count == 0) {
            // If every buffered wave was passed, the original starting point may already be unloaded.
            float spacing = difficultyManager.currentDifficulty != null ? difficultyManager.currentDifficulty.carOffsetModifier : 50;
            carSpawnPosition = _playerPosition + Vector3.forward * Mathf.Max(50, spacing) + Vector3.up;
        }
        while (rowsOfCars.Count < carBufferSize) {
            var previous = rowsOfCars.Count > 0 ? rowsOfCars[rowsOfCars.Count - 1] : null;
            var next = SpawnCarWave(previous);
            if (next == null || next.Count == 0 || ReferenceEquals(next, previous)) break;
            rowsOfCars.Add(next);
            hasSpawned = true;
        }
    }

    public int BufferedWaveCount => rowsOfCars.Count;
    public int TargetWaveCount => carBufferSize;
    public float FurthestCarDistance {
        get {
            float furthest = 0;
            foreach (var car in activeCars) if (car != null) furthest = Mathf.Max(furthest, car.transform.position.z - _playerPosition.z);
            return furthest;
        }
    }

    private List<DodgePoint> SpawnCarWave(List<DodgePoint> PrevPassPoints) {

        RaycastHit hit;

        if (PrevPassPoints == null) {
            if (!Physics.Raycast(new Ray(carSpawnPosition, Vector3.down), out hit, Mathf.Infinity, LayerMask.GetMask("Water"))) {
                return null;
            }
        }
        else {
            if (!Physics.Raycast(new Ray(PrevPassPoints.First().GetPosition(), Vector3.down), out hit, Mathf.Infinity, LayerMask.GetMask("Water"))) {
                Debug.DrawLine(PrevPassPoints.First().GetPosition(), _playerPosition, Color.red);
                return PrevPassPoints;
            }
        }

        Tile spawnTile = hit.transform.GetComponentInParent<Tile>();
        if (spawnTile == null || spawnTile.carLines == null || spawnTile.carLines.Length < 2) return PrevPassPoints;
        Transform[] carLines = spawnTile.carLines;
        Difficulty difficulty = difficultyManager.currentDifficulty;
        if (difficulty == null) return PrevPassPoints;

        // Determine the number of cars to spawn based on the given proportions
        int totalRows = difficulty.oneCarRow + difficulty.twoCarRow + difficulty.threeCarRow;
        int randomValue = UnityEngine.Random.Range(1, totalRows + 1);

        int carsToSpawn;
        if (randomValue <= difficulty.oneCarRow) {
            carsToSpawn = 1;
        }
        else if (randomValue <= difficulty.oneCarRow + difficulty.twoCarRow) {
            carsToSpawn = 2;
        }
        else {
            carsToSpawn = 3;
        }

        carsToSpawn = Mathf.Clamp(carsToSpawn, 1, carLines.Length - 1);

        // Shuffle the car lines and select the required number of lines
        List<Transform> shuffledLines = carLines.OrderBy(x => Guid.NewGuid()).ToList();

        if (PrevPassPoints != null) {
            List<Transform> previousSelectedLines = new List<Transform>();

            foreach (var point in PrevPassPoints) {
                foreach (var line in carLines) {
                    if (point.offsetObject.transform.position.x == line.position.x) {
                        previousSelectedLines.Add(line);
                    }
                }
            }

            previousSelectedLines = previousSelectedLines.Distinct().ToList();

            List<Transform> previouslyFreeLines = carLines.Except(previousSelectedLines).OrderBy(x => Guid.NewGuid()).ToList();

            shuffledLines = previouslyFreeLines.Concat(previousSelectedLines).ToList();
        }

        Transform[] selectedLines = shuffledLines.Take(carsToSpawn).OrderBy(x => Guid.NewGuid()).ToArray();
        Transform[] freeLines = shuffledLines.Where(line => !selectedLines.Contains(line)).ToArray();

        float lineOffset = carLines[1].transform.position.x - carLines[0].transform.position.x;
        float spawnOffset;

        if (PrevPassPoints == null) {
            spawnOffset = Mathf.Max(hit.transform.position.z, carSpawnPosition.z) + CalculateOffsetFromAngle(lineOffset, DegreesToRadians(90 - aiController.dodgeAngleLimit));
        }
        else {
            spawnOffset = PrevPassPoints.Last().GetPosition().z;

            foreach (DodgePoint point in PrevPassPoints) {
                if (Mathf.Sign(point.offset.z) > 0)
                    continue; // Skip points that are ahead of the current tile

                foreach (Transform line in freeLines) {
                    if (point.GetPosition().x == line.position.x) {
                        spawnOffset = Mathf.Max(spawnOffset, point.GetPosition().z);
                        spawnOffset += 10f; // Adjust the spacing as needed
                    }
                    else {
                        float xLength = Mathf.Abs(point.GetPosition().x - line.transform.position.x);
                        float angleOffset = CalculateOffsetFromAngle(xLength, DegreesToRadians(90 - aiController.dodgeAngleLimit));
                        spawnOffset = Mathf.Max(spawnOffset, point.GetPosition().z + angleOffset);
                    }
                }
            }
        }

        Dictionary<Transform, Obstacle> spawnedObstacles = new Dictionary<Transform, Obstacle>();

        // Plan the entire wave before instantiating anything. A retry cannot duplicate partial waves.
        var positions = new Dictionary<Transform, Vector3>();
        foreach (Transform line in selectedLines) {
            Vector3 spawnPosition = line.position;
            spawnPosition.z = spawnOffset + UnityEngine.Random.Range(-2.5f, 2.5f) + (PrevPassPoints == null ? 0 : difficulty.carOffsetModifier);
            if (!Physics.Raycast(spawnPosition + Vector3.up + Vector3.forward * 10f, Vector3.down, Mathf.Infinity, LayerMask.GetMask("Water")))
                return PrevPassPoints;
            positions.Add(line, spawnPosition);
        }
        foreach (Transform line in selectedLines) {
            Vector3 spawnPosition = positions[line];
            GameObject car = Instantiate(cars[UnityEngine.Random.Range(0, cars.Length)], spawnPosition, Quaternion.identity);
            car.transform.position += transform.forward * car.GetComponent<MeshRenderer>().bounds.size.z / 2;

            car.GetComponent<Car>().whichWall = 1;
            car.GetComponent<Obstacle>().line = line.GetComponent<Line>().number;

            activeCars.Add(car);

            spawnedObstacles.Add(line, car.GetComponent<Obstacle>());
        }

        if (enemy != null && activeEnemy == null && freeLines.Length > 0) {
            Vector3 spawnPosition = freeLines[UnityEngine.Random.Range(0, freeLines.Length)].transform.position;
            spawnPosition.z = spawnOffset + difficulty.carOffsetModifier;
            activeEnemy = Instantiate(enemy, spawnPosition, Quaternion.identity);
        }

        List<DodgePoint> passPoints = new List<DodgePoint>();

        for (int i = 0; i < carLines.Length; i++) {
            if (freeLines.Contains(carLines[i])) {
                DodgePoint furthestBackPoint = null;
                DodgePoint furthestFrontPoint = null;

                List<DodgePoint> leftCarPoints = new List<DodgePoint>();
                if (i != 0 && spawnedObstacles.ContainsKey(carLines[i - 1])) {
                    leftCarPoints = spawnedObstacles[carLines[i - 1]].GetDodgePoints2(1);
                }

                List<DodgePoint> rightCarPoints = new List<DodgePoint>();
                if (i != carLines.Length - 1 && spawnedObstacles.ContainsKey(carLines[i + 1])) {
                    rightCarPoints = spawnedObstacles[carLines[i + 1]].GetDodgePoints2(-1);
                }

                List<DodgePoint> combinedPoints = leftCarPoints.Concat(rightCarPoints).ToList();

                foreach (var point in combinedPoints) {
                    if (furthestBackPoint == null || point.GetPosition().z < furthestBackPoint.GetPosition().z) {
                        furthestBackPoint = point;
                    }
                    if (furthestFrontPoint == null || point.GetPosition().z > furthestFrontPoint.GetPosition().z) {
                        furthestFrontPoint = point;
                    }
                }

                if (furthestBackPoint != null) {
                    passPoints.Add(furthestBackPoint);
                    aiController.dodgePoints.Add(furthestBackPoint);

                }
                if (furthestFrontPoint != null) {
                    passPoints.Add(furthestFrontPoint);
                    aiController.dodgePoints.Add(furthestFrontPoint);
                }

            }
        }

        return passPoints;
    }

    private float CalculateOffsetFromAngle(float xLength, float radians) {
        return Mathf.Tan(radians) * xLength;
    }

    private float DegreesToRadians(float degrees) {
        return degrees * Mathf.PI / 180;
    }

}
