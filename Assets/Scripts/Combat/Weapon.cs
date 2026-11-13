using System;
using UnityEngine;

namespace CyberBiker.Combat {
    public readonly struct ShotRequest {
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public GameObject Owner { get; }
        public ShotRequest(Vector3 origin, Vector3 direction, GameObject owner) {
            Origin = origin;
            Direction = direction;
            Owner = owner;
        }
    }

    public abstract class Weapon {
        private float interval;
        private float lastShotTime = float.NegativeInfinity;
        public event Action Fired;

        protected Weapon(float interval) { SetInterval(interval); }

        public void SetInterval(float interval) { this.interval = Mathf.Max(0.01f, interval); }
        public bool CanFireAt(float time) => time >= lastShotTime + interval;

        public bool TryFire(ShotRequest request, float time) {
            if (!CanFireAt(time) || request.Direction.sqrMagnitude < 0.0001f) return false;
            if (!Fire(request)) return false;
            lastShotTime = time;
            Fired?.Invoke();
            return true;
        }

        protected abstract bool Fire(ShotRequest request);
    }
}
