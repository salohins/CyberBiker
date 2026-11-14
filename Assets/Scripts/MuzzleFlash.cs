using System.Collections.Generic;
using CyberBiker.Pooling;
using UnityEngine;

public class MuzzleFlash : MonoBehaviour {
    public int SpawnCount {get;private set;}
    [SerializeField] private GameObject muzzleFlash;
    private readonly List<(GameObject instance,int expires)> flashes=new List<(GameObject,int)>(8);
    private void Update(){
        for(int i=flashes.Count-1;i>=0;i--)if(Time.frameCount>=flashes[i].expires){PrefabPool.Release(flashes[i].instance);flashes.RemoveAt(i);}
    }
    public void Spawn(){
        SpawnCount++;
        var flash=PrefabPool.Rent(muzzleFlash,transform.TransformPoint(muzzleFlash.transform.localPosition),transform.rotation*muzzleFlash.transform.localRotation,transform);
        flash.transform.localEulerAngles=new Vector3(Random.Range(0,360),0,0);
        flashes.Add((flash,Time.frameCount+3));
    }
    private void OnDisable(){foreach(var flash in flashes)PrefabPool.ReleaseAfterHierarchyChange(flash.instance);flashes.Clear();}
}
