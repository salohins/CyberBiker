using UnityEngine;

namespace CyberBiker.Combat {
    public readonly struct DamageInfo {
        public float Amount { get; }
        public GameObject Source { get; }
        public DamageInfo(float amount, GameObject source = null) {
            Amount = amount;
            Source = source;
        }
    }

    public interface IDamageable {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo damage);
    }
}
