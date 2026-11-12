using UnityEngine;
using CyberBiker.Combat;

public class Bullet : MonoBehaviour {
    [Tooltip("Base projectile speed, before inheriting forward velocity from the shooter.")]
    [Min(0)] public float bulletSpeed = 30f;
    [Tooltip("Fallback damage for bullets spawned without a weapon. Weapon-fired bullets use the shooter's Shooting Damage setting.")]
    [SerializeField, Min(0)] private float damage = 10;
    [Tooltip("Maximum projectile lifetime in seconds.")]
    [SerializeField, Min(0.1f)] private float lifetime = 5;
    private GameObject owner;
    private bool consumed;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    public void Initialize(GameObject owner, float damage) {
        this.owner = owner;
        this.damage = damage;
        if (owner == null) return;
        var body = owner.GetComponent<Rigidbody>();
        if (body != null) bulletSpeed += Mathf.Max(0, Vector3.Dot(body.linearVelocity, transform.forward));
        foreach (var a in GetComponentsInChildren<Collider>())
            foreach (var b in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(a, b);
    }
    private void Start() { Destroy(gameObject, Mathf.Max(0.1f, lifetime)); }
    private void Update() {
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
    }
    private void OnTriggerEnter(Collider other) { Hit(other); }
    private void OnCollisionEnter(Collision collision) { Hit(collision.collider); }
    private void Hit(Collider other) {
        if (consumed || (owner != null && other.transform.IsChildOf(owner.transform))) return;
        var target = other.GetComponentInParent<IDamageable>();
        if (other.isTrigger && target == null) return;
        consumed = true;
        if (target != null && target.IsAlive) target.TakeDamage(new DamageInfo(damage, owner));
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Impact, transform.position);
        Destroy(gameObject);
    }
}
