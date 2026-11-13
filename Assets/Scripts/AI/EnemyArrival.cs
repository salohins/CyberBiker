using System;
using System.Collections.Generic;
using UnityEngine;

// A short, physical approach. Original materials and collision pairs are restored
// after arrival; no position correction or teleport is used to join the fight.
internal sealed class EnemyArrival : IDisposable {
    private struct Binding {public Renderer renderer;public Material[] originals;}
    private struct Pair {public Collider enemy,player;}
    private readonly Transform enemy,player;
    private readonly List<Binding> bindings=new List<Binding>();
    private readonly List<Material> materials=new List<Material>();
    private readonly List<Pair> contacts=new List<Pair>();
    private readonly float closingSpeed,fadeTime,passingSide;
    private float age;
    private bool restored;
    public bool Active {get;private set;}=true;
    public bool FromBehind {get;}
    public float Visibility {get;private set;}
    public float ForwardSpeed {get;private set;}
    public bool Finished=>!Active && restored && contacts.Count==0;
    public EnemyArrival(GameObject owner,GameObject rider,bool behind,float closing,float fade) {
        enemy=owner.transform;player=rider.transform;FromBehind=behind;
        passingSide=owner.transform.position.x>=rider.transform.position.x ? 1 : -1;
        closingSpeed=Mathf.Clamp(closing,10,120);fadeTime=Mathf.Max(.2f,fade);
        var body=rider.GetComponent<Rigidbody>();ForwardSpeed=Mathf.Max(0,(body!=null ? body.linearVelocity.z : 0)+(behind ? closingSpeed : -closingSpeed));
        var template=Resources.Load<Material>("EnemyArrivalFade");
        if(template!=null)foreach(var renderer in owner.GetComponentsInChildren<Renderer>(true)) {
            if(!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))continue;
            var originals=renderer.sharedMaterials;var faded=new Material[originals.Length];
            for(int i=0;i<originals.Length;i++) {
                var source=originals[i];if(source==null)continue;
                var material=new Material(source){shader=template.shader,name=source.name+" (arrival)",renderQueue=3000};
                material.SetFloat("_ArrivalVisibility",0);
                material.SetFloat("_ArrivalReflection",source.IsKeywordEnabled("_REFLECTION") ? 1 : 0);
                material.SetFloat("_ArrivalRim",source.IsKeywordEnabled("_RIM") ? 1 : 0);
                faded[i]=material;materials.Add(material);
            }
            bindings.Add(new Binding{renderer=renderer,originals=originals});renderer.sharedMaterials=faded;
        }
        if(behind)foreach(var a in owner.GetComponentsInChildren<Collider>(true))foreach(var b in rider.GetComponentsInChildren<Collider>(true))
            if(a.enabled && b.enabled && !a.isTrigger && !b.isTrigger && !Physics.GetIgnoreCollision(a,b)){
                Physics.IgnoreCollision(a,b,true);contacts.Add(new Pair{enemy=a,player=b});
            }
    }
    public Vector3 ConstrainOvertake(Vector3 current,Vector3 next,float dt) {
        float gap=current.z-player.position.z;
        if(Active && FromBehind && gap> -25 && gap<12 && (next.x-player.position.x)*passingSide<2.5f)
            next.x=Mathf.MoveTowards(current.x,player.position.x+passingSide*2.5f,12*dt);
        return next;
    }
    public void Tick(float dt,float desiredGap,float playerSpeed) {
        age+=dt;Visibility=Mathf.SmoothStep(0,1,age/fadeTime);
        if(!restored) {
            foreach(var material in materials)material.SetFloat("_ArrivalVisibility",Visibility);
            if(Visibility>=.999f)RestoreMaterials();
        }
        if(Active) {
            float gap=enemy.position.z-player.position.z;
            float targetSpeed=Mathf.Max(0,playerSpeed+Mathf.Clamp((desiredGap-gap)*2.5f,-closingSpeed,closingSpeed));
            ForwardSpeed=Mathf.MoveTowards(ForwardSpeed,targetSpeed,60*dt);
            if(gap>5 && Mathf.Abs(gap-desiredGap)<3 && Visibility>=.999f)Active=false;
        }
        for(int i=contacts.Count-1;i>=0;i--) {
            var pair=contacts[i];
            if(pair.enemy==null || pair.player==null){contacts.RemoveAt(i);continue;}
            if(Active){Physics.IgnoreCollision(pair.enemy,pair.player,true);continue;}
            if(pair.enemy.bounds.Intersects(pair.player.bounds))continue;
            Physics.IgnoreCollision(pair.enemy,pair.player,false);contacts.RemoveAt(i);
        }
    }
    private void RestoreMaterials() {
        foreach(var binding in bindings)if(binding.renderer!=null)binding.renderer.sharedMaterials=binding.originals;
        foreach(var material in materials)if(material!=null)UnityEngine.Object.Destroy(material);
        materials.Clear();bindings.Clear();restored=true;
    }
    public void Dispose() {
        if(!restored)RestoreMaterials();
        foreach(var pair in contacts)if(pair.enemy!=null && pair.player!=null)Physics.IgnoreCollision(pair.enemy,pair.player,false);
        contacts.Clear();
    }
}
