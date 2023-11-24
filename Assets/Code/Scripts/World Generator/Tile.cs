using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using AI;

public class Tile : MonoBehaviour
{
    public Transform[] carLines;
    public bool carsSpawned;

    private AIController aiController;
    private WorldGenerator wg;    
    private GameObject player;

    private Vector3 nextTileSpawnPos;

    private float carMinInterval;
    private float carMaxInterval;

    void Awake()
    {
        wg = FindFirstObjectByType<WorldGenerator>();
        aiController = FindFirstObjectByType<AIController>();
        player = GameObject.FindGameObjectWithTag("Player");        
        nextTileSpawnPos = transform.GetChild(transform.childCount - 1).transform.position;

        carMinInterval = wg.carMinSpawnInterval;
        carMaxInterval = wg.carMaxSpawnInterval;
    }

    public List<Vector3> SpawnCars(List<Vector3> lastCarPos) {        
        List<Vector3> nextCarPos = new List<Vector3>();
        List<Vector3> carSpawnPos = new List<Vector3>();
        int emptyLine = Random.Range(0, carLines.Length);
        if (player.transform.position.z > wg.carSpawnStartDistance) {
             for (int i = 0; i < carLines.Length; i++) {
                
                float spawnZ = transform.position.z;
                bool nextTile = false;

                if (i == emptyLine)
                    continue;

                if (lastCarPos != null) {
                    foreach (Vector3 pos in lastCarPos.OrderBy(obj => obj.z)) {
                        if (pos.x == carLines[i].position.x && pos.z < nextTileSpawnPos.z) {
                            //SpawnCar(pos);
                            carSpawnPos.Add(pos);

                            spawnZ = pos.z;
                        }
                        else if (pos.x == carLines[i].position.x && pos.z > nextTileSpawnPos.z) {
                            nextTile = true;
                            nextCarPos.Add(pos);
                        }
                    }

                    if (nextTile) {
                        continue;
                    }

                    spawnZ += aiController.passLength * 1.5f; // only if enemies are around
                }

                

                spawnZ += GetInterval();
         

                while (spawnZ < nextTileSpawnPos.z) {
                    Vector3 spawnPos = new Vector3(carLines[i].position.x, carLines[i].position.y, spawnZ);

                    //SpawnCar(spawnPos);
                    carSpawnPos.Add(spawnPos);


                    spawnZ += GetInterval();
                }

                nextCarPos.Add(new Vector3(carLines[i].position.x, carLines[i].position.y, spawnZ));
            }
        }

        foreach (Vector3 pos in carSpawnPos.OrderBy(item => item.z)) {
            for (int i = 0; i < carLines.Length; i++) {
                if (carLines[i].transform.position.x == pos.x) {                    
                    //if (Random.Range(0, 2) == 0) {
                        GameObject car = Instantiate(wg.cars[Random.Range(0, wg.cars.Length)], pos, Quaternion.identity);


                        car.GetComponent<Car>().whichWall = i > 1 ? 1 : -1;
                        car.GetComponent<Obstacle>().SetLine();

                        //aiController.activeCars.Add(car);
                        aiController.UpdateDodgePoints(car);
                        
                    //}
                    break;

                }
            }

                       
        }

        return nextCarPos;
    } 

    private float GetInterval() => Random.Range(carMinInterval, carMaxInterval);

    private void SpawnCar(Vector3 position, int wallDirection) {
        GameObject car = Instantiate(wg.cars[Random.Range(0, wg.cars.Length)], position, Quaternion.identity);


        car.GetComponent<Car>().whichWall = wallDirection;
        car.GetComponent<Obstacle>().SetLine();

        //aiController.activeCars.Add(car);
        aiController.UpdateDodgePoints(car);
    }
}
