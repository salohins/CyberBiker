using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AI;

public class Tile : MonoBehaviour {
    // Array of car spawn positions on this tile
    public Transform[] carLines;

    // Flag indicating if cars have been spawned on this tile
    public bool carsSpawned;

    // References to key components in the game
    private AIController aiController;
    private WorldGenerator wg;
    private GameObject player;

    // Position where the next tile will be spawned
    private Vector3 nextTileSpawnPos;

    // Minimum and maximum intervals for spawning cars
    private float carMinInterval;
    private float carMaxInterval;

    
    // Called when the script instance is being loaded
    void Awake() {
        // Find the WorldGenerator component in the scene
        wg = FindFirstObjectByType<WorldGenerator>();

        // Find the AIController component in the scene
        aiController = FindFirstObjectByType<AIController>();

        // Find the player GameObject by its tag
        player = GameObject.FindGameObjectWithTag("Player");

        // Set the spawn position for the next tile using the position of the last child of this tile
        nextTileSpawnPos = transform.GetChild(transform.childCount - 1).transform.position;

        // Set the car spawn interval using values from the WorldGenerator
    }

    // Method for spawning cars on the tile based on previous car positions
    /*public List<Vector3> SpawnCars(List<Vector3> lastCarPos) {
        List<Vector3> nextCarPos = new List<Vector3>();  // List to store positions of cars for the next tile
        List<Vector3> carSpawnPos = new List<Vector3>(); // List to store car positions for the current tile

        // Randomly choose a car line to leave empty (no car will be spawned there)
        int emptyLine = Random.Range(0, carLines.Length - 1);

        // Check if the player has moved past the car spawn start distance
        if (player.transform.position.z > wg.carSpawnStartDistance) {
            // Iterate over each car line on this tile
            for (int i = 0; i < carLines.Length; i++) {

                float spawnZ = transform.position.z; // Initialize spawn position at the start of the tile
                bool nextTile = false; // Flag to determine if the car should be spawned on the next tile

                if (i == emptyLine)
                    continue; // Skip spawning on the empty line

                if (lastCarPos != null) {
                    // Adjust spawn position based on positions from the previous tile
                    foreach (Vector3 pos in lastCarPos.OrderBy(obj => obj.z)) {
                        if (pos.x == carLines[i].position.x && pos.z < nextTileSpawnPos.z) {
                            carSpawnPos.Add(pos);
                            spawnZ = pos.z;
                        }
                        else if (pos.x == carLines[i].position.x && pos.z > nextTileSpawnPos.z) {
                            nextTile = true;
                            nextCarPos.Add(pos);
                        }
                    }

                    if (nextTile) {
                        continue; // Skip to the next line if the car should be spawned on the next tile
                    }

                    //spawnZ += aiController.GetPassLength(); // Adjust spawn position if enemies are around
                }

                spawnZ += GetInterval(); // Apply a random interval to the spawn position

                // Continue spawning cars along the line until reaching the end of the tile
                while (spawnZ < nextTileSpawnPos.z) {
                    Vector3 spawnPos = new Vector3(carLines[i].position.x, carLines[i].position.y, spawnZ);
                    carSpawnPos.Add(spawnPos); // Add the spawn position to the list

                    spawnZ += GetInterval(); // Increment the spawn position by a random interval
                }

                nextCarPos.Add(new Vector3(carLines[i].position.x, carLines[i].position.y, spawnZ)); // Add the position for the next tile
            }
        }

        // Instantiate cars at the calculated positions
        foreach (Vector3 pos in carSpawnPos.OrderBy(item => item.z)) {
            for (int i = 0; i < carLines.Length; i++) {
                if (carLines[i].transform.position.x == pos.x) {
                    GameObject car = Instantiate(wg.cars[Random.Range(0, wg.cars.Length)], pos, Quaternion.identity);

                    // Determine which wall (left or right) the car is facing
                    car.GetComponent<Car>().whichWall = i > 1 ? 1 : -1;

                    // Set the car's line in the obstacle
                    car.GetComponent<Obstacle>().SetLine();

                    // Update AI dodge points with the new car's position
                    //aiController.UpdateDodgePoints(car);

                    break; // Stop once the car is spawned on the correct line
                }
            }
        }

        return nextCarPos; // Return the list of positions for the next tile
    }*/

    // Alternative method for spawning cars with dodge points
    /*public List<DodgePoint> SpawnCars2(List<DodgePoint> PrevPassPoints) {
        if (player.transform.position.z < wg.carSpawnStartDistance)
            return null; // Don't spawn cars if the player hasn't reached the start distance

        // Shuffle the car lines to randomize spawning positions
        List<Transform> shuffledLines = carLines.OrderBy(x => System.Guid.NewGuid()).ToList();

        int carsToSpawn = 2; // Number of cars to spawn

        // Select the lines where cars will be spawned
        Transform[] selectedLines = shuffledLines.Take(carsToSpawn).ToArray();

        // Remaining lines where cars will not be spawned
        Transform[] freeLines = shuffledLines.Where(line => !selectedLines.Contains(line)).ToArray();

        // Calculate the offset for car spawning based on line spacing and dodge angle
        float lineOffset = carLines[1].transform.position.x - carLines[0].transform.position.x;
        float spawnOffset = CalculateOffsetFromAngle(lineOffset, DegreesToRadians(90 - aiController.dodgeAngleLimit));

        // Adjust the spawn offset based on previous pass points if they exist
        if (PrevPassPoints != null) {
            float maxOffsetLength = 0;
            Vector3 spawnOffsetStart = Vector3.zero;

            foreach (DodgePoint point in PrevPassPoints) {
                if (Mathf.Sign(point.offset.z) > 0)
                    continue; // Skip points that are ahead of the current tile

                foreach (Transform line in freeLines) {
                    if (point.GetPosition().x == line.position.x) {
                        spawnOffsetStart = point.GetPosition();
                        spawnOffset += point.GetPosition().z;
                        continue;
                    }

                    // Calculate angle offset and find the maximum offset length
                    float xLength = Mathf.Abs(point.GetPosition().x - line.transform.position.x);
                    float angleOffset = CalculateOffsetFromAngle(xLength, DegreesToRadians(90 - aiController.dodgeAngleLimit));

                    if (angleOffset > maxOffsetLength) {
                        maxOffsetLength = angleOffset;
                        spawnOffset = point.GetPosition().z + angleOffset;
                    }
                }
            }
        }
        else {
            spawnOffset = transform.position.z; // Default spawn position at the start of the tile
        }

        // Dictionary to keep track of spawned obstacles
        Dictionary<Transform, Obstacle> spawnedObstacles = new Dictionary<Transform, Obstacle>();

        // Spawn cars at the selected lines
        foreach (Transform line in selectedLines) {
            Vector3 spawnPosition = line.transform.position;
            spawnPosition.z = spawnOffset;

            GameObject car = Instantiate(wg.cars[Random.Range(0, wg.cars.Length - 1)], spawnPosition, Quaternion.identity);
            car.transform.position += transform.forward * car.GetComponent<MeshRenderer>().bounds.size.z / 2;

            car.GetComponent<Car>().whichWall = 1;
            car.GetComponent<Obstacle>().line = line.GetComponent<Line>().number;

            spawnedObstacles.Add(line, car.GetComponent<Obstacle>());
        }

        List<DodgePoint> passPoints = new List<DodgePoint>();

        // Add dodge points for each free line
        for (int i = 0; i < carLines.Length; i++) {
            if (freeLines.Contains(carLines[i])) {
                DodgePoint furthestBackPoint = null;
                DodgePoint furthestFrontPoint = null;

                // Get dodge points for the car to the left, if any
                List<DodgePoint> leftCarPoints = new List<DodgePoint>();
                if (i != 0 && spawnedObstacles.ContainsKey(carLines[i - 1])) {
                    leftCarPoints = spawnedObstacles[carLines[i - 1]].GetDodgePoints2(1);
                }

                // Get dodge points for the car to the right, if any
                List<DodgePoint> rightCarPoints = new List<DodgePoint>();
                if (i != carLines.Length - 1 && spawnedObstacles.ContainsKey(carLines[i + 1])) {
                    rightCarPoints = spawnedObstacles[carLines[i + 1]].GetDodgePoints2(-1);
                }

                // Combine points from left and right
                List<DodgePoint> combinedPoints = leftCarPoints.Concat(rightCarPoints).ToList();

                // Find the furthest back and front points
                foreach (var point in combinedPoints) {
                    if (furthestBackPoint == null || point.GetPosition().z < furthestBackPoint.GetPosition().z) {
                        furthestBackPoint = point;
                    }
                    if (furthestFrontPoint == null || point.GetPosition().z > furthestFrontPoint.GetPosition().z) {
                        furthestFrontPoint = point;
                    }
                }

                // Add only the furthest back and furthest front points
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

        return passPoints; // Return the list of dodge points for the current tile
    } */


    // Calculate the offset based on the angle between lines
    private float CalculateOffsetFromAngle(float xLength, float degrees) {
        float cosDegrees = Mathf.Cos(degrees); // Convert degrees to radians and get the cosine

        float h = xLength / cosDegrees; // Calculate the hypotenuse length

        float c = Mathf.Sqrt(Mathf.Pow(h, 2) - Mathf.Pow(xLength, 2)); // Calculate the other adjacent side

        return c; // Return the calculated offset
    }

    // Helper method to convert degrees to radians
    private float DegreesToRadians(float degrees) => degrees * Mathf.PI / 180.0f;

    // Get a random interval between car spawns
    private float GetInterval() => Random.Range(carMinInterval, carMaxInterval);

    // Method to spawn a car at a specific position with a given wall direction
    /*private void SpawnCar(Vector3 position, int wallDirection) {
        GameObject car = Instantiate(wg.cars[Random.Range(0, wg.cars.Length)], position, Quaternion.identity);

        car.GetComponent<Car>().whichWall = wallDirection;
        car.GetComponent<Obstacle>().SetLine();

        //aiController.UpdateDodgePoints(car);
    }*/
}
    