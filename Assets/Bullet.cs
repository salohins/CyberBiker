using UnityEngine;
using CyberBiker.Combat;
using System.Collections.Generic;
using CyberBiker.Pooling;

public class Bullet : MonoBehaviour, IPooledObject {
    private static readonly List<Bullet> activeShots = new List<Bullet>(64);
    public static IReadOnlyList<Bullet> ActiveShots => activeShots;
    public bool IsHostile => hostile && !consumed && isActiveAndEnabled;
    public Vector3 Velocity => transform.forward*bulletSpeed;
    public GameObject Owner => owner;
    public int Lease {get;private set;}
    public bool FollowsRider {get;private set;}
    private PlayerController evadedPlayer;
    private BulletAirWake airWake;
    private void OnEnable(){if(!activeShots.Contains(this))activeShots.Add(this);}
    private void OnDisable(){activeShots.Remove(this);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetShots(){activeShots.Clear();}
    public bool IgnoresPlayer(PlayerController player) => evadedPlayer!=null && evadedPlayer==player;
    public void Evade(PlayerController player){evadedPlayer=player;}
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
    private bool hostile,nearMissChecked,closeDodgeChecked,hasContact;
    private Vector3 contactPoint,contactNormal;
    private CyberBiker.Player.ShootingFeedback shooterFeedback;
    private readonly List<Collider> ownerShapes=new List<Collider>(16);
    private readonly List<(Collider projectile,Collider owner)> ignored=new List<(Collider,Collider)>(16);
    private readonly RaycastHit[] hits = new RaycastHit[16];
    public void Initialize(GameObject owner, float damage) {
        Cache();ClearIgnored();
        this.owner = owner;
        this.damage = damage;
        Lease++;evadedPlayer=null;
        FollowsRider=transform.parent!=null && transform.parent.GetComponent<BulletWorld>()!=null;
        hostile=owner!=null && owner.GetComponent<Enemy>()!=null;
        if(hostile){
            if(airWake==null)airWake=GetComponent<BulletAirWake>() ?? gameObject.AddComponent<BulletAirWake>();
            var manager=CyberBiker.Player.PlayerProjectileEvade.Active!=null ? CyberBiker.Player.PlayerProjectileEvade.Active.Manager : FindAnyObjectByType<Gameplay.GameplayManager>();
            airWake.Configure(manager!=null ? manager.ProjectileCombat : null);
        }else airWake?.Clear();
        shooterFeedback=owner!=null ? owner.GetComponent<CyberBiker.Player.ShootingFeedback>() : null;
        nearMissChecked=closeDodgeChecked=hasContact=false;
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
    public void OnPoolRent(GameObject prefab){Cache();var settings=prefab.GetComponent<Bullet>();bulletSpeed=settings.bulletSpeed;damage=settings.damage;lifetime=settings.lifetime;owner=null;consumed=false;lifetimeStarted=false;hostile=nearMissChecked=closeDodgeChecked=hasContact=false;FollowsRider=false;shooterFeedback=null;}
    public void OnPoolReturn(){ClearIgnored();owner=null;consumed=true;shooterFeedback=null;evadedPlayer=null;airWake?.Clear();}
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
            if(evadedPlayer!=null && candidate.transform.IsChildOf(evadedPlayer.transform))continue;
            if (candidate.isTrigger && candidate.GetComponentInParent<IDamageable>() == null) continue;
            if (hits[i].distance < nearestDistance) { nearest = candidate; nearestDistance = hits[i].distance;contactPoint=hits[i].point;contactNormal=hits[i].normal; }
        }
        if(hostile && (!nearMissChecked || !closeDodgeChecked)){
            var feedback=CyberBiker.Player.ShootingFeedback.Active;
            if(feedback!=null){
                bool playerHit=nearest!=null && nearest.GetComponentInParent<PlayerController>()!=null;
                if(playerHit)nearMissChecked=closeDodgeChecked=true;
                else {
                    var end=nearest!=null ? contactPoint : transform.position+transform.forward*distance;
                    if(!closeDodgeChecked)closeDodgeChecked=feedback.CheckCloseDodge(transform.position,end,Time.deltaTime,this);
                    int before=feedback.NearMissCount;
                    if(!nearMissChecked && feedback.CheckNearMiss(transform.position,end,Time.deltaTime,this)){
                        nearMissChecked=true;if(feedback.NearMissCount>before)CyberBiker.Player.PlayerProjectileEvade.Active?.ObservePassedShot(this);
                    }
                }
            }
        }
        hasContact=nearest!=null;
        if (nearest != null) Hit(nearest);
        if (!consumed) transform.position += transform.forward * distance;
        if(expired && !consumed){consumed=true;PrefabPool.Release(gameObject);}
    }
    private void OnTriggerEnter(Collider other) { Hit(other); }
    private void OnCollisionEnter(Collision collision) { Hit(collision.collider); }
    private void Hit(Collider other) {
        if (!isActiveAndEnabled || consumed || (owner != null && other.transform.IsChildOf(owner.transform))) return;
        if(evadedPlayer!=null && other.transform.IsChildOf(evadedPlayer.transform))return;
        var target = other.GetComponentInParent<IDamageable>();
        if (other.isTrigger && target == null) return;
        consumed = true;
        Vector3 point=hasContact ? contactPoint : other.ClosestPoint(transform.position);
        Vector3 normal=hasContact ? contactNormal : -transform.forward;
        if (target != null && target.IsAlive){
            var state=target as IDamageState;float previous=state!=null ? state.CurrentHealth : 0;
            target.TakeDamage(new DamageInfo(damage, owner, point, normal, transform.forward));
            if(shooterFeedback!=null && state!=null && state.CurrentHealth<previous){
                var component=target as Component;var enemy=component!=null ? component.GetComponent<Enemy>() : null;
                shooterFeedback.ConfirmHit(component!=null ? component.transform : other.transform,point,normal,enemy!=null && enemy.IsFinisherReady);
            }
        }
        transform.position=point;
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Impact, transform.position);
        PrefabPool.Release(gameObject);
    }
    private void OnDestroy(){ClearIgnored();}
}
