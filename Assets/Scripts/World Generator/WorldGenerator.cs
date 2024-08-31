using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI;
using System;
using UnityEngine.Animations;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

public class WorldGenerator : MonoBehaviour {
    [Header("World Generation Settings")]
    [SerializeField] private Biome[] biomes;

    [Tooltip("-1 for random")]
    [SerializeField] private int startBiomeIndex;

    [SerializeField] private GameObject startTile;

    [SerializeField] private int bufferSize;

    [SerializeField] private GameObject ground;

    private int _biomeIndex;
    private int _biomeLength;

    private GameObject[] _activeTiles;
    private Vector3 _playerPosition;

    private GameObject player;
    private AIController aiController;

    [Header("Car Spawn Settings")]
    [SerializeField]
    private GameObject[] cars;

    [Tooltip("When cars start spawning")]
    public float carSpawnStartDistance;

    [SerializeField]
    private float carOffsetModifier;

    [SerializeField]
    private int carBufferSize;        
    [SerializeField]
    private int oneCarRow = 0;
    [SerializeField]
    private int twoCarRow = 0;    
    [SerializeField]
    private int threeCarRow = 1;

    private Vector3 carSpawnPosition;
    
    private bool carsSpawned;

    [SerializeField]
    private GameObject collectiblePrefab;
    [SerializeField]
    private float collectibleOffset = 5;
    [SerializeField]
    private float collectibleMaxLineDistance = 100;



    private List<DodgePoint> newRow;
    

    private List<GameObject> activeCars;
    private List<Vector3> carSpawnPositionLine;

    // List to store rows of DodgePoints
    private List<List<DodgePoint>> rowsOfCars;        

    void Start() {
        activeCars = new List<GameObject>();
        carSpawnPositionLine = new List<Vector3>();

        player = GameObject.FindGameObjectWithTag("Player");
        
        aiController = FindFirstObjectByType<AIController>();        

        ChangeBiome(startBiomeIndex);

        carSpawnPosition = player.transform.position + Vector3.forward * carSpawnStartDistance + Vector3.up;

        _activeTiles = new GameObject[bufferSize];
        rowsOfCars = new List<List<DodgePoint>>();

        for (int i = 0; i < _activeTiles.Length; i++) {
            if (i == 0) {
                _activeTiles[i] = Instantiate(startTile, transform.position, transform.rotation, transform);
            }
            else {
                Vector3 spawnPosition = _activeTiles[i - 1].transform.GetChild(_activeTiles[i - 1].transform.childCount - 1).transform.position;
                _activeTiles[i] = Instantiate(biomes[_biomeIndex].GetRandomTile(), spawnPosition, transform.rotation, transform);
            }
        }
    }

    void Update() {
        _playerPosition = GameObject.FindGameObjectWithTag("Player").transform.position;
        

        

        Vector3 shiftTriggerPosition = _activeTiles[1].transform.position + Vector3.forward * 10f;

        if (_playerPosition.z > shiftTriggerPosition.z) {
            StartCoroutine(ShiftTiles());
        }

        // Check if cars are spawned and start the spawning process
        if (Physics.Raycast(carSpawnPosition, Vector3.down) && !carsSpawned) {

            newRow = SpawnCarWave(null);
            rowsOfCars.Add(newRow);  // Add the row to the list

            for (int i = 0; i < carBufferSize - 1; i++) {
                newRow = SpawnCarWave(newRow);

                if (newRow != rowsOfCars.Last())
                    rowsOfCars.Add(newRow);  // Add the row to the list
            }

            carsSpawned = true;
        }
        

        // Remove dodge points for rows that are behind the player
        aiController.dodgePoints.RemoveAll(point => point.GetPosition().z < _playerPosition.z);

        for (int i = activeCars.Count - 1; i >= 0; i--) {
            GameObject car = activeCars[i];

            // Check if the car object is still valid (i.e., not destroyed)
            if (car != null) {
                if (car.transform.position.z < player.transform.position.z - 20f) {
                    // Destroy the car object
                    Destroy(car);

                    // Remove it from the activeCars list
                    activeCars.RemoveAt(i);
                }
            }
            else {
                // If the car is null (maybe already destroyed elsewhere), remove it from the list
                activeCars.RemoveAt(i);
            }
        }

        // Check if the player has passed the first row of cars
        if (carsSpawned && rowsOfCars.Count > 0 && _playerPosition.z > rowsOfCars[0].Max(dp => dp.GetPosition().z)) { 
            if (carSpawnPositionLine.Count == 0) {
                newRow = SpawnCarWave(newRow);  // Spawn a new row

                if (newRow != rowsOfCars.Last()) {
                    rowsOfCars.Add(newRow);  // Add it to the list
                    rowsOfCars.RemoveAt(0);  // Remove the first row    
                }                
            }                    
        }

        if (carSpawnPositionLine.Count > 0 && Physics.Raycast(carSpawnPositionLine.First() + Vector3.up + Vector3.forward * 10, Vector3.down)) {
            Debug.Log("Delayed Wave");
            carSpawnPositionLine.Remove(carSpawnPositionLine[0]);
            newRow = SpawnCarWave(newRow);  // Spawn a new row
            rowsOfCars.Add(newRow);  // Add it to the list
            //rowsOfCars.RemoveAt(0);  // Remove the first row  
        }        
    }

    private List<DodgePoint> SpawnCarWave(List<DodgePoint> PrevPassPoints) {

        if (carSpawnPositionLine.Count > 0) {
            return PrevPassPoints;
        }

        RaycastHit hit;

        if (PrevPassPoints == null) {
            if (!Physics.Raycast(new Ray(carSpawnPosition, Vector3.down), out hit)) {
                Debug.LogWarning("Raycast did not hit a surface!");
                return null;
            }
        }
        else {
            if (!Physics.Raycast(new Ray(PrevPassPoints.First().GetPosition(), Vector3.down), out hit)) {
                Debug.DrawLine(PrevPassPoints.First().GetPosition(), _playerPosition, Color.red);
                Debug.LogWarning("Raycast did not hit a surface!");                                
                Debug.Break();          
                //carSpawnPositionLine.Add(PrevPassPoints.Last().GetPosition());
                //return PrevPassPoints;
            }            
        }

        GameObject spawnTile = hit.transform.gameObject;
        Transform[] carLines = spawnTile.GetComponent<Tile>().carLines;

        // Determine the number of cars to spawn based on the given proportions
        int totalRows = oneCarRow + twoCarRow + threeCarRow;
        int randomValue = UnityEngine.Random.Range(1, totalRows + 1);

        int carsToSpawn;
        if (randomValue <= oneCarRow) {
            carsToSpawn = 1;
        }
        else if (randomValue <= oneCarRow + twoCarRow) {
            carsToSpawn = 2;
        }
        else {
            carsToSpawn = 3;
        }

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
            spawnOffset = hit.transform.position.z + CalculateOffsetFromAngle(lineOffset, DegreesToRadians(90 - aiController.dodgeAngleLimit));
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

        Vector3 outsidePosition = Vector3.zero;
        bool collectibleSpawned = false;

        foreach (Transform line in selectedLines) {
            Vector3 spawnPosition = line.transform.position;

            // Add randomness to the z offset to avoid spawning cars directly next to each other
            float randomOffset = UnityEngine.Random.Range(-2.5f, 2.5f); // Adjust the range for more or less randomness
            spawnPosition.z = spawnOffset + randomOffset + (PrevPassPoints == null ? 0 : carOffsetModifier);

            if (!Physics.Raycast(spawnPosition + Vector3.up + Vector3.forward * 10f, Vector3.down)) {
                outsidePosition = spawnPosition;
                break;
            }

            GameObject car = Instantiate(cars[UnityEngine.Random.Range(0, cars.Length)], spawnPosition, Quaternion.identity);
            car.transform.position += transform.forward * car.GetComponent<MeshRenderer>().bounds.size.z / 2;

            car.GetComponent<Car>().whichWall = 1;
            car.GetComponent<Obstacle>().line = line.GetComponent<Line>().number;

            /*if (Physics.Raycast(new Ray(spawnPosition + Vector3.up * 2, Vector3.back), out hit)) {
                SpawnCollectibles(spawnPosition + Vector3.up * 2 - Vector3.forward * 30, hit.point + Vector3.forward * 10, car);
            }*/

            if (PrevPassPoints != null && !collectibleSpawned && (car.GetComponent<Obstacle>().GetBack() + Vector3.back * 50f + Vector3.up).z > PrevPassPoints.Last().GetPosition().z) {
                SpawnCollectibles(car.GetComponent<Obstacle>().GetBack() + Vector3.back * 50f + Vector3.up, PrevPassPoints.Last().GetPosition(), car);
                collectibleSpawned = true;
            }


            activeCars.Add(car);

            spawnedObstacles.Add(line, car.GetComponent<Obstacle>());
        }

        if (outsidePosition != Vector3.zero) {
            carSpawnPositionLine.Add(outsidePosition);
            return PrevPassPoints;
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

    void SpawnCollectibles(Vector3 pointA, Vector3 pointB, GameObject parent) {        

        float maxDistance;        

        


        Instantiate(collectiblePrefab, pointA, collectiblePrefab.transform.rotation, parent.transform);

        pointA += Vector3.back * collectibleOffset;

        pointB.y = pointA.y;
        pointB.x = pointA.x;

        maxDistance = pointA.z - pointB.z;

        int numCollectibles = Mathf.FloorToInt(maxDistance / collectibleOffset);

        

        if (numCollectibles > 0) {
            for (int i = 0; i <= numCollectibles; i++) {
                Vector3 spawnPosition = Vector3.Lerp(pointA, pointB, i / (float)numCollectibles);
                Instantiate(collectiblePrefab, spawnPosition, collectiblePrefab.transform.rotation, parent.transform);
            }
        }
    }

    IEnumerator ShiftTiles() {
        Destroy(_activeTiles[0]);

        for (int i = 1; i < _activeTiles.Length; i++) {
            _activeTiles[i - 1] = _activeTiles[i];
        }

        yield return null;

        Vector3 spawnPosition = _activeTiles[bufferSize - 2].transform.GetChild(_activeTiles[bufferSize - 2].transform.childCount - 1).transform.position;

        _activeTiles[bufferSize - 1] = Instantiate(biomes[_biomeIndex].GetRandomTile(), spawnPosition, transform.rotation, transform);
    }

    private void ChangeBiome(int index) {
        if (_biomeIndex == -1 && biomes.Length > 1) {
            int currentIndex = _biomeIndex;

            //Calculating next random biome
            _biomeIndex = UnityEngine.Random.Range(0, biomes.Length - 1);

            //if biome index same as current
            if (_biomeIndex == currentIndex) {
                if (_biomeIndex == biomes.Length - 1)
                    _biomeIndex--;
                else
                    _biomeIndex++;
            }
        }
        else {
            _biomeIndex = startBiomeIndex;
        }

        Biome _biome = biomes[_biomeIndex];
        _biomeLength = UnityEngine.Random.Range(_biome.minLength, _biome.maxLength);
    }
}
