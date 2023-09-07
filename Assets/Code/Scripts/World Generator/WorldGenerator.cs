using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private GameObject ground;
    [SerializeField] private Biome[] biomes;

    [Tooltip("-1 for random")]
    [SerializeField] private int startBiomeIndex;

    [SerializeField] private GameObject startTile;

    [SerializeField] private int bufferSize;

    private int _biomeIndex;
    private int _biomeLength;

    private GameObject[] _activeTiles;
    private Vector3 _playerPosition;

    private GameObject player;


    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");

        ChangeBiome(startBiomeIndex);

        _activeTiles = new GameObject[bufferSize];

        for (int i = 0; i < _activeTiles.Length; i++) {
            if (i == 0) {
                _activeTiles[i] = Instantiate(startTile, transform.position, transform.rotation, transform);
            }
            else {
                Vector3 spawnPosition = _activeTiles[i - 1].transform.position + transform.forward * getTileSize(_activeTiles[i - 1]);
                _activeTiles[i] = Instantiate(biomes[_biomeIndex].GetRandomTile(), spawnPosition, transform.rotation, transform);
            }
        }
    }
    
    void Update() {
         _playerPosition = GameObject.FindGameObjectWithTag("Player").transform.position;

        Vector3 shiftTriggerPosition = _activeTiles[1].transform.position;

        if (_playerPosition.z > shiftTriggerPosition.z) {
            StartCoroutine(ShiftTiles());
        }

        ground.transform.position = new Vector3(player.transform.position.x, 0, player.transform.position.z);
    }

    private float getTileSize(GameObject tile) {
        Vector3 tileTileMeshSize = tile.GetComponent<MeshRenderer>().bounds.size;
        Vector3 tileTileMeshScale = tile.transform.localScale;
        
        return tileTileMeshSize.z;
    }

    IEnumerator ShiftTiles() {
        Destroy(_activeTiles[0]);

        for (int i = 1; i < _activeTiles.Length; i++) {
            _activeTiles[i - 1] = _activeTiles[i];
        }

        yield return null;        

        Vector3 spawnPosition = _activeTiles[bufferSize - 2].transform.position + transform.forward * getTileSize(_activeTiles[bufferSize - 2]);

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
