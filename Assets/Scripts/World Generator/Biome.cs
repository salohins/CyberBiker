using System;
using System.Collections.Generic;
using UnityEngine;

public class Biome : MonoBehaviour {
    [SerializeField] private GameObject[] tiles;
    [SerializeField] private bool randomizeTiles;
    [SerializeField] private GameObject[] startTiles;
    [SerializeField] private GameObject[] endTiles;
    [SerializeField] public int minLength;
    [SerializeField] public int maxLength;
    public GameObject GetRandomTile() => GetRandomTile(null);
    public GameObject GetRandomTile(Tile previous) {
        var compatible = new List<GameObject>();
        if (tiles != null) foreach (var candidate in tiles) {
            if (candidate == null) continue;
            var tile = candidate.GetComponent<Tile>();
            if (tile != null && (previous == null || tile.CanFollow(previous))) compatible.Add(candidate);
        }
        if (compatible.Count == 0) throw new InvalidOperationException($"Biome {name} has no tile compatible with {previous?.ExitType}.");
        return compatible[randomizeTiles ? UnityEngine.Random.Range(0, compatible.Count) : 0];
    }
}
