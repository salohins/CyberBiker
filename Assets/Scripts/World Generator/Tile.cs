using UnityEngine;

public enum RouteSurface { Street, HighStreet, Roof }

public class Tile : MonoBehaviour {
    public Transform[] carLines;
    public bool carsSpawned;
    [SerializeField] private RouteSurface entryType;
    [SerializeField] private RouteSurface exitType;
    [SerializeField] private Transform exitSocket;
    public RouteSurface EntryType => entryType;
    public RouteSurface ExitType => exitType;
    public Transform ExitSocket {
        get {
            if (exitSocket != null) return exitSocket;
            exitSocket = transform.Find("NextTileSpawnPos");
            // Older prefabs used child order; retain it until they are migrated.
            if (exitSocket == null && transform.childCount > 0) exitSocket = transform.GetChild(transform.childCount - 1);
            return exitSocket;
        }
    }
    public bool CanFollow(Tile previous) => previous != null && previous.ExitType == EntryType;
}
