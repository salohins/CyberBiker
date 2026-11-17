using CyberBiker.Combat;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(14000),DisallowMultipleComponent]
public sealed class EnemyGunEffects : MonoBehaviour {
    private sealed class Muzzle {public Transform anchor;public GameObject flash;public Light light;public SpriteRenderer[] sprites;public float remaining;public int bornFrame;public Quaternion localShotRotation;}
    private readonly Muzzle[] muzzles=new Muzzle[2];
    private Enemy enemy;
    private GameplayManager manager;
    private ParticleSystem smoke;
    private Material flashMaterial;
    public int ShotCount {get;private set;}
    public int SmokeCount=>smoke!=null ? smoke.particleCount : 0;
    public int VisibleFlashCount {get {int n=0;foreach(var slot in muzzles)if(slot!=null && slot.flash!=null && slot.flash.activeSelf)n++;return n;}}
    public Vector3 LastMuzzlePosition {get;private set;}
    public ParticleSystem Smoke=>smoke;
    private ProjectileCombatSettings Settings=>manager.ProjectileCombat;
    private void Awake(){
        enemy=GetComponent<Enemy>();manager=FindAnyObjectByType<GameplayManager>();
        flashMaterial=Resources.Load<Material>("GunMuzzleFlash");
        var existing=transform.Find("Enemy gun smoke");if(existing!=null){smoke=existing.GetComponent<ParticleSystem>();if(smoke!=null)return;}
        var root=new GameObject("Enemy gun smoke");root.transform.SetParent(transform,false);smoke=root.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=smoke.main;main.playOnAwake=false;main.loop=false;main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;main.startSpeed=0;main.startSize3D=true;
        var emission=smoke.emission;emission.enabled=false;var shape=smoke.shape;shape.enabled=false;
        var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.35f,1,1.6f));
        var fade=smoke.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.8f,.83f,.88f),0),new GradientColorKey(new Color(.45f,.5f,.58f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.08f),new GradientAlphaKey(.35f,.4f),new GradientAlphaKey(0,1)});fade.color=gradient;
        var noise=smoke.noise;noise.enabled=true;noise.strength=.18f;noise.frequency=1.6f;noise.quality=ParticleSystemNoiseQuality.Low;noise.scrollSpeed=.7f;
        var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("EnemyMuzzleSmoke");renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.maxParticleSize=.12f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private Muzzle GetMuzzle(int index,Transform anchor){
        var slot=muzzles[index];if(slot==null){slot=new Muzzle();muzzles[index]=slot;}slot.anchor=anchor;if(slot.flash!=null)return slot;
        var prefab=Resources.Load<GameObject>("EnemyMuzzleFlash");if(prefab==null)return slot;
        var existing=transform.Find("Enemy muzzle flash "+index);slot.flash=existing!=null ? existing.gameObject : Instantiate(prefab,transform);slot.flash.name="Enemy muzzle flash "+index;slot.light=slot.flash.GetComponentInChildren<Light>(true);slot.sprites=slot.flash.GetComponentsInChildren<SpriteRenderer>(true);slot.flash.SetActive(false);return slot;
    }
    public void Fire(int index,Transform anchor,Vector3 direction){
        if(manager==null || enemy==null || smoke==null)Awake();
        if(flashMaterial==null)flashMaterial=Resources.Load<Material>("GunMuzzleFlash");
        if(manager==null || manager.gameState!=GameState.Playing || enemy==null || !enemy.IsAlive || anchor==null)return;
        ShotCount++;LastMuzzlePosition=anchor.position;direction=direction.normalized;
        var slot=GetMuzzle(index,anchor);slot.remaining=Settings.enemyFlashDuration;slot.bornFrame=Time.frameCount;
        var rotation=Quaternion.AngleAxis((ShotCount*137)%360,direction)*Quaternion.FromToRotation(Vector3.left,direction);
        slot.localShotRotation=Quaternion.Inverse(anchor.rotation)*rotation;
        UpdateFlash(slot,true);
        if(!smoke.isPlaying)smoke.Play();
        var smokeRenderer=smoke.GetComponent<ParticleSystemRenderer>();if(smokeRenderer.sharedMaterial==null)smokeRenderer.sharedMaterial=Resources.Load<Material>("EnemyMuzzleSmoke");
        var wind=smoke.forceOverLifetime;wind.enabled=true;wind.space=ParticleSystemSimulationSpace.World;wind.z=-Settings.smokeWindSweep;wind.y=1;
        Vector3 velocity=enemy.DrivingVelocity;if(velocity.sqrMagnitude<1)velocity=Vector3.forward*enemy.ForwardSpeed;
        for(int i=0;i<Settings.enemySmokeCount;i++){
            float angle=(ShotCount*.73f+i*2.39996f);var spread=new Vector3(Mathf.Sin(angle),.3f+Mathf.Cos(angle)*.25f,Mathf.Cos(angle));
            var emit=new ParticleSystem.EmitParams{position=anchor.position+direction*(.06f+i*.025f),velocity=velocity*.88f+direction*(1.6f+i*.25f)+spread*.65f,startLifetime=Settings.enemySmokeLifetime*(.85f+i*.035f),startSize3D=new Vector3(.7f+i*.07f,.4f+i*.035f,1),startColor=new Color(.8f,.83f,.88f,.8f),rotation=10+i*5,randomSeed=(uint)(1241+ShotCount*71+i)};
            smoke.Emit(emit,1);
        }
    }
    private void UpdateFlash(Muzzle slot,bool show){
        if(slot.flash==null)return;slot.flash.SetActive(show);if(!show || slot.anchor==null)return;
        slot.flash.transform.SetPositionAndRotation(slot.anchor.position,slot.anchor.rotation*slot.localShotRotation);
        var scale=transform.lossyScale;slot.flash.transform.localScale=new Vector3(Settings.enemyFlashScale/Mathf.Max(.01f,Mathf.Abs(scale.x)),Settings.enemyFlashScale/Mathf.Max(.01f,Mathf.Abs(scale.y)),Settings.enemyFlashScale/Mathf.Max(.01f,Mathf.Abs(scale.z)));
        if(slot.light!=null){slot.light.intensity=Settings.enemyFlashLight;slot.light.range=4;slot.light.shadows=LightShadows.None;}
        foreach(var sprite in slot.sprites){if(flashMaterial!=null)sprite.sharedMaterial=flashMaterial;sprite.color=new Color(Settings.enemyFlashBrightness,Settings.enemyFlashBrightness,Settings.enemyFlashBrightness,.8f);}
    }
    private void LateUpdate(){
        bool playing=manager!=null && manager.gameState==GameState.Playing && enemy!=null && enemy.IsAlive;
        foreach(var slot in muzzles)if(slot!=null){if(playing && Time.frameCount!=slot.bornFrame)slot.remaining=Mathf.Max(0,slot.remaining-Time.deltaTime);UpdateFlash(slot,playing && slot.remaining>0);}
    }
    private void OnDisable(){foreach(var slot in muzzles)if(slot!=null){slot.remaining=0;if(slot.flash!=null)slot.flash.SetActive(false);}if(smoke!=null)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
}
