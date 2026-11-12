using System;

namespace CyberBiker.Combat {
    // Domain state stays independent of colliders, animations and UI.
    public sealed class Health {
        public float Maximum { get; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0;
        public event Action<float, float> Changed;
        public event Action Died;

        public Health(float maximum) {
            if (maximum <= 0 || float.IsNaN(maximum) || float.IsInfinity(maximum))
                throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }

        public bool TakeDamage(float amount) => TakeDamage(new DamageInfo(amount));

        public bool TakeDamage(DamageInfo damage) {
            if (!IsAlive || damage.Amount <= 0 || float.IsNaN(damage.Amount)) return false;
            Current = Math.Max(0, Current - damage.Amount);
            Changed?.Invoke(Current, Maximum);
            if (!IsAlive) Died?.Invoke();
            return true;
        }

        public bool Heal(float amount) {
            // Pickups cannot resurrect a finished run.
            if (!IsAlive || Current >= Maximum || amount <= 0 || float.IsNaN(amount)) return false;
            Current = Math.Min(Maximum, Current + amount);
            Changed?.Invoke(Current, Maximum);
            return true;
        }
    }
}
