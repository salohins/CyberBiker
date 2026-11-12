using System.Collections;
using UnityEngine;
using AI;

public class WorldGenerator : MonoBehaviour {
    [SerializeField] private Biome[] biomes;
    [SerializeField] private int startBiomeIndex;
    [SerializeField] private GameObject startTile;
    [SerializeField] private int bufferSize;
    [SerializeField] private GameObject ground;
    [SerializeField] private GameObject[] cars;
    [SerializeField] private GameObject enemy;
    public float carSpawnStartDistance;
    [SerializeField] private int carBufferSize;
    private int _biomeIndex;
    private int _biomeLength;
    private GameObject[] _activeTiles;
    private GameObject player;
    private bool shifting;
    private void Start() {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || startTile == null || biomes == null || biomes.Length == 0) {
            Debug.LogError("WorldGenerator requires a player, start tile and biome.", this);
            enabled = false; return;
        }
        bufferSize = Mathf.Max(2, bufferSize);
        ChangeBiome(startBiomeIndex);
        _activeTiles = new GameObject[bufferSize];
        _activeTiles[0] = Instantiate(startTile, transform.position, transform.rotation, transform);
        for (int i = 1; i < bufferSize; i++) _activeTiles[i] = SpawnAfter(_activeTiles[i - 1]);
        var traffic = gameObject.AddComponent<TrafficSpawner>();
        traffic.Initialize(cars, enemy, player, FindAnyObjectByType<AIController>(),
            FindAnyObjectByType<DifficultyManager>(), carSpawnStartDistance, carBufferSize);
    }
    private GameObject SpawnAfter(GameObject previous) {
        var previousTile = previous.GetComponent<Tile>();
        var exit = previousTile.ExitSocket;
        var prefab = biomes[_biomeIndex].GetRandomTile(previousTile);
        return Instantiate(prefab, exit.position, transform.rotation, transform);
    }
    private void Update() {
        if (!shifting && player.transform.position.z > _activeTiles[1].transform.position.z + 10f)
            StartCoroutine(ShiftTiles());
    }
    private IEnumerator ShiftTiles() {
        shifting = true;
        Destroy(_activeTiles[0]);
        for (int i = 1; i < bufferSize; i++) _activeTiles[i - 1] = _activeTiles[i];
        yield return null;
        _activeTiles[bufferSize - 1] = SpawnAfter(_activeTiles[bufferSize - 2]);
        shifting = false;
    }
    private void ChangeBiome(int index) {
        _biomeIndex = index < 0 ? Random.Range(0, biomes.Length) : Mathf.Clamp(index, 0, biomes.Length - 1);
        var biome = biomes[_biomeIndex];
        _biomeLength = Random.Range(biome.minLength, biome.maxLength);
    }
}
