using System.Collections.Generic;
using CyberBiker.Pooling;
using UnityEngine;

public class MuzzleFlash : MonoBehaviour {
    public int SpawnCount {get;private set;}
    [SerializeField] private GameObject muzzleFlash;
    private readonly List<(GameObject instance,int expires,float remaining,int born)> flashes=new List<(GameObject,int,float,int)>(8);
    private Gameplay.GameplayManager manager;
    private PlayerController player;
    public GameObject LastFlash {get;private set;}
    public float FlashDuration=>manager!=null ? manager.ProjectileCombat.playerFlashDuration : .075f;
    private void Awake(){manager=FindAnyObjectByType<Gameplay.GameplayManager>();player=GetComponentInParent<PlayerController>();}
    private void Update(){
        for(int i=flashes.Count-1;i>=0;i--){var lease=flashes[i];if(Time.frameCount!=lease.born)lease.remaining-=Time.deltaTime;
            if(Time.frameCount>=lease.expires && lease.remaining<=0){PrefabPool.Release(lease.instance);flashes.RemoveAt(i);}else flashes[i]=lease;}
    }
    public void Spawn(){
        Vector3 direction=transform.right;if(player!=null)player.TryGetPistolAim(out _,out direction);Spawn(direction);
    }
    public void Spawn(Vector3 direction){
        if(muzzleFlash==null)return;if(manager==null)manager=FindAnyObjectByType<Gameplay.GameplayManager>();
        SpawnCount++;
        direction=direction.sqrMagnitude>.001f ? direction.normalized : transform.right;
        var rotation=Quaternion.AngleAxis((SpawnCount*137)%360,direction)*Quaternion.FromToRotation(Vector3.left,direction);
        var flash=PrefabPool.Rent(muzzleFlash,transform.position,rotation,transform);LastFlash=flash;
        float size=manager!=null ? manager.ProjectileCombat.playerFlashScale : .12f;
        var scale=transform.lossyScale;flash.transform.localScale=new Vector3(size/Mathf.Max(.000001f,Mathf.Abs(scale.x)),size/Mathf.Max(.000001f,Mathf.Abs(scale.y)),size/Mathf.Max(.000001f,Mathf.Abs(scale.z)));
        var visual=flash.GetComponent<MuzzleFlashVisual>();if(visual==null)visual=flash.AddComponent<MuzzleFlashVisual>();visual.Configure(manager!=null ? manager.ProjectileCombat.playerFlashBrightness : .95f,manager!=null ? manager.ProjectileCombat.playerFlashLight : 1.1f);
        flashes.Add((flash,Time.frameCount+3,Mathf.Max(.025f,FlashDuration),Time.frameCount));
    }
    private void OnDisable(){foreach(var flash in flashes)PrefabPool.ReleaseAfterHierarchyChange(flash.instance);flashes.Clear();}
}
