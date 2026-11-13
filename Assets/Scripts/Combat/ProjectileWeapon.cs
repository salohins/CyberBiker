using UnityEngine;
using CyberBiker.Pooling;

namespace CyberBiker.Combat {
    public sealed class ProjectileWeapon : Weapon {
        private readonly GameObject projectile;
        private readonly Transform parent;
        private float damage;
        public ProjectileWeapon(GameObject projectile, float interval, float damage, Transform parent = null)
            : base(interval) {
            this.projectile = projectile;
            this.parent = parent;
            SetDamage(damage);
        }

        public void SetDamage(float damage) { this.damage = Mathf.Max(0, damage); }

        protected override bool Fire(ShotRequest request) {
            if (projectile == null) return false;
            var instance = PrefabPool.Rent(projectile, request.Origin,
                Quaternion.LookRotation(request.Direction), parent);
            var bullet = instance.GetComponent<Bullet>();
            if (bullet != null) bullet.Initialize(request.Owner, damage);
            if (request.Owner != null) CyberBiker.Audio.SoundEvents.Emit(
                request.Owner.GetComponent<PlayerController>() != null ? CyberBiker.Audio.SoundCue.PlayerShot : CyberBiker.Audio.SoundCue.EnemyShot,
                request.Origin);
            return true;
        }
    }
}
