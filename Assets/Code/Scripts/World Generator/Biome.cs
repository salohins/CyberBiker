using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Biome : MonoBehaviour
{
    [Header("Tiles")]
    [SerializeField] private GameObject[] tiles;
    [SerializeField] private GameObject[] startTiles;
    [SerializeField] private GameObject[] endTiles;

    [SerializeField] public int minLength;
    [SerializeField] public int maxLength;

    public GameObject GetRandomTile() => tiles[Random.Range(0, tiles.Length - 1)];
}
