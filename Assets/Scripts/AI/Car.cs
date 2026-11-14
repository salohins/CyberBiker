using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay;
using AI;
using CyberBiker.Combat;
using CyberBiker.Pooling;

public class Car : MonoBehaviour, IDamageable, IDamageState, IPooledObject
{
    private static readonly List<Car> activeCars = new List<Car>(64);
    // Track lifetime, then filter active GameObjects at the call site. A disabled
    // movement component must still participate in pass-by sound detection.
    public static IReadOnlyList<Car> Instances => activeCars;
    private void OnEnable() { if(!activeCars.Contains(this))activeCars.Add(this); }
    private Health vitality;
    public bool IsAlive => vitality != null && vitality.IsAlive;

    [SerializeField] public float speed = 10f;
    [SerializeField] public int whichWall;

    [Header("Damage and Destruction")]
    [Tooltip("Starting health, applied when the car spawns. Legacy 0 values keep the existing 5 HP fallback.")]
    [SerializeField, Min(0)] private int health;
    [Tooltip("Whether this car can receive damage. Enabled by default to preserve existing cars.")]
    [SerializeField] private bool destructible = true;
    [Tooltip("Play the assigned explosion effect when health reaches zero. Does not add area damage.")]
    [SerializeField] private bool explodeOnDeath = true;
    [Tooltip("Explosion particle prefab. The car hides its mesh and collider on death even if no effect is assigned.")]
    [SerializeField] private ParticleSystem explosion;
    public float CurrentHealth => vitality != null ? vitality.Current : (health > 0 ? health : 5);


    private bool hit;

    private GameObject player;
    private GameplayManager gm;

    private AIController aiController;
    private BoxCollider box, playerBox;
    private int wallMask;
    private ParticleSystem deathEffect;

    public int lineNumber;

    // Read transforms directly: AI runs between physics updates, so collider.bounds
    // can still describe the previous position of a transform-driven traffic car.
    public bool TryGetTrafficShape(float dt,out Bounds bounds,out Vector3 step) {
        bounds=default;step=Vector3.zero;
        if(box==null || !gameObject.activeInHierarchy || !IsAlive || !box.enabled || box.isTrigger)return false;
        var shape=box.transform;var half=box.size*.5f;
        var x=shape.TransformVector(Vector3.right*half.x);var y=shape.TransformVector(Vector3.up*half.y);var z=shape.TransformVector(Vector3.forward*half.z);
        bounds=new Bounds(shape.TransformPoint(box.center),new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        if(isActiveAndEnabled && (player==null || bounds.max.z>=player.transform.position.z-(playerBox!=null ? playerBox.bounds.extents.z : 0)))
            step=transform.forward*Mathf.Min(Mathf.Max(0,speed)*dt,1);
        return true;
    }

    private void Awake() {
        activeCars.Add(this);
        box=GetComponent<BoxCollider>();
        wallMask=LayerMask.GetMask("WorldObject");
    }

    void Start()
    {
        if(vitality!=null)return;
        transform.SetParent(null);
        InitializeReferences();ResetHealth();
    }
    private void InitializeReferences(){
        if(box==null){box=GetComponent<BoxCollider>();wallMask=LayerMask.GetMask("WorldObject");}
        if(player==null){player=GameObject.FindGameObjectWithTag("Player");playerBox=player.GetComponent<BoxCollider>();}
        if(gm==null)gm=FindAnyObjectByType<GameplayManager>();
        if(aiController==null)aiController=FindAnyObjectByType<AIController>();
    }
    private void ResetHealth(){
        if(vitality!=null)vitality.Died-=Die;
        vitality = new Health(health > 0 ? health : 5);
        vitality.Died += Die;
    }
    public void OnPoolRent(GameObject prefab){InitializeReferences();var settings=prefab.GetComponent<Car>();speed=settings.speed;whichWall=settings.whichWall;lineNumber=settings.lineNumber;health=settings.health;destructible=settings.destructible;explodeOnDeath=settings.explodeOnDeath;hit=false;ResetHealth();}
    public void OnPoolReturn(){CyberBiker.Audio.GameAudio.Active?.ForgetCar(this);if(deathEffect!=null){if(deathEffect.transform.IsChildOf(transform)){deathEffect.gameObject.SetActive(false);Destroy(deathEffect.gameObject);}deathEffect=null;}}

    public void GetDamage() { TakeDamage(new DamageInfo(1)); }
    public void TakeDamage(DamageInfo damage) { if (destructible) vitality?.TakeDamage(damage); }
    private void Die() {
        CyberBiker.Audio.SoundEvents.Emit(explodeOnDeath ? CyberBiker.Audio.SoundCue.Explosion : CyberBiker.Audio.SoundCue.Impact, transform.position);

        if (explodeOnDeath && explosion != null) deathEffect=Instantiate(explosion, transform);
        box.enabled = false;
        GetComponent<MeshRenderer>().enabled = false;
    }
    private void OnDestroy() { activeCars.Remove(this); if (vitality != null) vitality.Died -= Die; }

    public Vector3 GetFrontPosition() {
        return transform.position + transform.forward * (box.bounds.size.z / 2);
    }

    public Vector3 GetBackPosition() {
        return transform.position - transform.forward * (box.bounds.size.z / 2);
    }

    private void Update() {
        if (hit) {
            speed = Mathf.Lerp(speed, 0, Time.deltaTime * 10f);

        }
    }

    private void OnCollisionEnter(Collision collision) {

        if (collision.collider.GetComponentInParent<PlayerController>() != null) {
            var finisher=collision.collider.GetComponentInParent<CyberBiker.Player.BikeSwordCombat>();
            if(finisher!=null && finisher.IgnoresTraffic(box))return;
            hit = true;
            box.enabled = false;
        }
    }

    void FixedUpdate()
    {

        // Skip irrelevant cars before issuing either wall raycast.
        if (!IsAlive || player == null || player.transform.position.y - 15f > transform.position.y) return;
        if (transform.position.z + box.bounds.size.z / 2 < player.transform.position.z - playerBox.bounds.size.z / 2) return;

        RaycastHit hit;

        if (!Physics.Raycast(new Ray(transform.position, transform.right * whichWall), out hit, 100f, wallMask)) {

            return;
        }

        Vector3 perp = Vector3.Cross(hit.normal, Vector3.up);
        Vector3 targetDir = Vector3.Project(transform.forward, perp).normalized;

        RaycastHit hit2;

        if (Physics.Raycast(new Ray(transform.position, -hit.normal), out hit2, 100f, wallMask)) {
            Vector3 predictPos = transform.position + targetDir;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(predictPos - transform.position),
                5f * Time.deltaTime
            );

            Debug.DrawLine(transform.position, hit2.point);

            transform.position = Vector3.MoveTowards(transform.position, predictPos, speed * Time.deltaTime);
        }
    }
}
