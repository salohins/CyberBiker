using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private int linePosition;

    private WorldGenerator wg;
    private int spawnRatio;
    private float minSpawnInterval;
    private float maxSpawnInterval;
    private float spawnDistance;
    private GameObject[] carArray;
    private GameObject player;

    private GameObject currentCar;

    // Start is called before the first frame update
    void Start()
    {
        wg = FindFirstObjectByType<WorldGenerator>();
        player = GameObject.FindGameObjectWithTag("Player");

        carArray = wg.cars;
        spawnRatio = wg.carSpawnRatio;
        minSpawnInterval = wg.carMinSpawnInterval;
        maxSpawnInterval = wg.carMaxSpawnInterval;
        spawnDistance = wg.carSpawnStartDistance;

        transform.position = new Vector3(transform.position.x, transform.position.y, spawnDistance);

        //Instantiate(wg.cars[Random.Range(0, wg.cars.Length)], transform.position, transform.rotation);
    }

    private GameObject spawnCar() {
        if (Random.Range(0, spawnRatio) == 0) {
            GameObject car = Instantiate(carArray[Random.Range(0, carArray.Length)], transform.position, transform.rotation);
            car.GetComponent<Car>().whichWall = linePosition;
            car.GetComponent<Car>().speed = Random.Range(wg.carMaxSpeed, wg.carMaxSpeed);

            return car;
        } else
            return null;
    }

    float a;

    // Update is called once per frame
    void FixedUpdate()
    {
        for (int i = 0; i < (player.transform.position.y - 5f > transform.position.y ? 10 : 1); i++) {
            if (player.transform.position.z < transform.position.z && Vector3.Distance(transform.position, player.transform.position) < (player.transform.position.y - 5f > transform.position.y ? 100 : 300) ) {

                transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z + Random.Range(minSpawnInterval, maxSpawnInterval));
                currentCar = null;

                RaycastHit hit;

                if (Physics.Raycast(transform.position + Vector3.up / 2, -Vector3.up, out hit)) {
                    transform.position = hit.point;
                    if (Physics.Raycast(transform.position + Vector3.up * 20f, transform.right)) {

                        currentCar = spawnCar();
                    }
                }
            }
        }

        if (currentCar != null)
            transform.position = new Vector3(transform.position.x, transform.position.y, currentCar.transform.position.z);
    }
}
