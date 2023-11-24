using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private GameObject[] carSpawners;
    [SerializeField] private GameObject ground;
    [SerializeField] private Biome[] biomes;

    [Header("Car Spawn Settings")]
    public GameObject[] cars;
    public float carMinSpeed;
    public float carMaxSpeed;
    [Tooltip("When cars start spawning")]
    public float carSpawnStartDistance;
    [Tooltip("Minimum interval between cars")]
    public float carMinSpawnInterval;
    [Tooltip("Maximun interval between cars")]
    public float carMaxSpawnInterval;
    [Tooltip("Car probability to spawn. 1 to ? the higher the number the lower chance of spawning. 1 - 100%, 2 - 50%, 3-33% and so on")]
    public int carSpawnRatio;    

    [Tooltip("-1 for random")]
    [SerializeField] private int startBiomeIndex;

    [SerializeField] private GameObject startTile;

    [SerializeField] private int bufferSize;

    private int _biomeIndex;
    private int _biomeLength;

    private GameObject[] _activeTiles;
    private Vector3 _playerPosition;

    private GameObject player;

    private List<Vector3> carSpawnPos;


    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        carSpawnPos = new List<Vector3>();

        ChangeBiome(startBiomeIndex);

        _activeTiles = new GameObject[bufferSize];

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

        foreach (GameObject tile in _activeTiles) {
            if (Vector3.Distance(tile.transform.position, player.transform.position) < 400 && !tile.GetComponent<Tile>().carsSpawned) {
                carSpawnPos = tile.GetComponent<Tile>().SpawnCars(carSpawnPos);
                tile.GetComponent<Tile>().carsSpawned = true;
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
            _biomeIndex = Random.Range(0, biomes.Length - 1);

            //if biome index same as current
            if (_biomeIndex == currentIndex) {
                if (_biomeIndex == biomes.Length - 1)
                    _biomeIndex--;
                else
                    _biomeIndex++;
            }
        } else {
            _biomeIndex = startBiomeIndex;
        }

        Biome _biome = biomes[_biomeIndex];
        _biomeLength = Random.Range(_biome.minLength, _biome.maxLength);
    }      
}
