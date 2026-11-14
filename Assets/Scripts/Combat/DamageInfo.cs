using UnityEngine;

namespace CyberBiker.Combat {
    public readonly struct DamageInfo {
        public float Amount { get; }
        public GameObject Source { get; }
        public bool HasContact { get; }
        public Vector3 ContactPoint { get; }
        public Vector3 ContactNormal { get; }
        public Vector3 TravelDirection { get; }
        public DamageInfo(float amount, GameObject source = null) {
            Amount = amount;
            Source = source;
            HasContact = false;
            ContactPoint = ContactNormal = TravelDirection = Vector3.zero;
        }
        public DamageInfo(float amount, GameObject source, Vector3 point, Vector3 normal, Vector3 direction) {
            Amount = amount;
            Source = source;
            HasContact = true;
            ContactPoint = point;
            ContactNormal = normal;
            TravelDirection = direction;
        }
    }

    public interface IDamageable {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo damage);
    }
    // Optional readback for confirmed-hit presentation. Damage receivers keep
    // ownership of immunity, stagger limits and health changes.
    public interface IDamageState { float CurrentHealth { get; } }
}
