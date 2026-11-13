using UnityEngine;
using CyberBiker.Pooling;

public enum RouteSurface { Street, HighStreet, Roof }

public class Tile : MonoBehaviour, IPooledObject {
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
    private Pickup[] pickups;
    public void OnPoolRent(GameObject prefab){carsSpawned=false;if(pickups==null)pickups=GetComponentsInChildren<Pickup>(true);foreach(var pickup in pickups)if(pickup!=null)pickup.ResetForReuse();}
    public void OnPoolReturn(){}
    public bool TryGetLaneFrame(out RoadLaneFrame frame){
        frame=default;if(carLines==null || carLines.Length<2 || ExitSocket==null)return false;
        var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)return false;
        var origin=transform.position;var right=Vector3.Cross(Vector3.up,forward);float min=float.PositiveInfinity,max=float.NegativeInfinity;
        foreach(var line in carLines)if(line!=null){float offset=Vector3.Dot(line.position-origin,right);min=Mathf.Min(min,offset);max=Mathf.Max(max,offset);}
        float length=Vector3.Dot(ExitSocket.position-origin,forward);if(max-min<.1f || length<=0)return false;
        var lease=GetComponent<PooledInstance>();frame=new RoadLaneFrame{tile=this,origin=origin,forward=forward,right=right,min=min,max=max,length=length,lease=lease,generation=lease!=null ? lease.Generation : 0};return true;
    }
}

[System.Serializable]
public struct RoadLaneFrame {
    public Tile tile;
    public Vector3 origin,forward,right;
    public float min,max,length;
    public PooledInstance lease;
    public uint generation;
    public bool IsCurrent=>tile!=null && (lease==null || (lease.IsRented && lease.Generation==generation));
    public float ForwardOffset(Vector3 position)=>Vector3.Dot(position-origin,forward);
    public bool ContainsForward(Vector3 position){float z=ForwardOffset(position);return z>=0 && z<=length;}
    public float ForwardDistance(Vector3 position){float z=ForwardOffset(position);return z<0 ? -z : z>length ? z-length : 0;}
    public Vector3 ConstrainStep(Vector3 current,Vector3 next){
        float before=Vector3.Dot(current-origin,right),after=Vector3.Dot(next-origin,right);
        float low=before<min ? before : min,high=before<min ? min : before>max ? before : max;
        if(before>max)low=max;
        return next+right*(Mathf.Clamp(after,low,high)-after);
    }
}
