using UnityEngine;
using CyberBiker.Combat;
using System.Collections.Generic;
using CyberBiker.Pooling;

public class Bullet : MonoBehaviour, IPooledObject {
    [Tooltip("Base projectile speed, before inheriting forward velocity from the shooter.")]
    [Min(0)] public float bulletSpeed = 30f;
    [Tooltip("Fallback damage for bullets spawned without a weapon. Player-fired bullets use GameplayManager's Pistol Damage Per Shot; enemy-fired bullets use the enemy's Shooting Damage.")]
    [SerializeField, Min(0)] private float damage = 10;
    [Tooltip("Maximum projectile lifetime in seconds.")]
    [SerializeField, Min(0.1f)] private float lifetime = 5;
    private GameObject owner;
    private bool consumed;
    private bool lifetimeStarted;
    private float remainingLifetime;
    private Collider[] shapes;
    private Rigidbody projectileBody;
    private readonly List<Collider> ownerShapes=new List<Collider>(16);
    private readonly List<(Collider projectile,Collider owner)> ignored=new List<(Collider,Collider)>(16);
    private readonly RaycastHit[] hits = new RaycastHit[16];
    public void Initialize(GameObject owner, float damage) {
        Cache();ClearIgnored();
        this.owner = owner;
        this.damage = damage;
        // Travel and damage are swept in Update. A solid dynamic collider can
        // otherwise depenetrate against a bike and shove it sideways before Hit.
        if(projectileBody!=null){projectileBody.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;projectileBody.isKinematic=true;}
        foreach(var shape in shapes)shape.isTrigger=true;
        if (owner == null) return;
        var body = owner.GetComponent<Rigidbody>();
        if (body != null) bulletSpeed += Mathf.Max(0, Vector3.Dot(body.linearVelocity, transform.forward));
        owner.GetComponentsInChildren(false,ownerShapes);
        foreach (var a in shapes)foreach (var b in ownerShapes)if(!Physics.GetIgnoreCollision(a,b)){Physics.IgnoreCollision(a,b);ignored.Add((a,b));}
    }
    private void Cache(){if(shapes!=null)return;shapes=GetComponentsInChildren<Collider>();projectileBody=GetComponent<Rigidbody>();}
    private void ClearIgnored(){foreach(var pair in ignored)if(pair.projectile!=null && pair.owner!=null)Physics.IgnoreCollision(pair.projectile,pair.owner,false);ignored.Clear();ownerShapes.Clear();}
    public void OnPoolRent(GameObject prefab){Cache();var settings=prefab.GetComponent<Bullet>();bulletSpeed=settings.bulletSpeed;damage=settings.damage;lifetime=settings.lifetime;owner=null;consumed=false;lifetimeStarted=false;}
    public void OnPoolReturn(){ClearIgnored();owner=null;consumed=true;}
    private void Update() {
        if(consumed)return;
        bool expired=false;
        // Start the original scaled-time lifetime on this lease's first update.
        if(!lifetimeStarted){remainingLifetime=Mathf.Max(.1f,lifetime);lifetimeStarted=true;}
        else {remainingLifetime-=Time.deltaTime;expired=remainingLifetime<=0;}
        float distance = Time.deltaTime * bulletSpeed;
        // Sweep the travelled segment so fast shots also hit thin, static colliders.
        int count = Physics.RaycastNonAlloc(transform.position, transform.forward, hits, distance, ~0, QueryTriggerInteraction.Collide);
        Collider nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++) {
            var candidate = hits[i].collider;
            if (candidate.transform.IsChildOf(transform) || (owner != null && candidate.transform.IsChildOf(owner.transform))) continue;
            if (candidate.isTrigger && candidate.GetComponentInParent<IDamageable>() == null) continue;
            if (hits[i].distance < nearestDistance) { nearest = candidate; nearestDistance = hits[i].distance; }
        }
        if (nearest != null) Hit(nearest);
        if (!consumed) transform.position += transform.forward * distance;
        if(expired && !consumed){consumed=true;PrefabPool.Release(gameObject);}
    }
    private void OnTriggerEnter(Collider other) { Hit(other); }
    private void OnCollisionEnter(Collision collision) { Hit(collision.collider); }
    private void Hit(Collider other) {
        if (!isActiveAndEnabled || consumed || (owner != null && other.transform.IsChildOf(owner.transform))) return;
        var target = other.GetComponentInParent<IDamageable>();
        if (other.isTrigger && target == null) return;
        consumed = true;
        if (target != null && target.IsAlive) target.TakeDamage(new DamageInfo(damage, owner));
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Impact, transform.position);
        PrefabPool.Release(gameObject);
    }
    private void OnDestroy(){ClearIgnored();}
}
