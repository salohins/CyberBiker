using System;
using UnityEngine;

namespace CyberBiker.Audio {
    public enum SoundCue { PlayerShot, EnemyShot, Impact, Explosion, Pickup, Crash, UiClick, EnemyFinisherReady, EnemyFinisherWarning, PistolHitConfirm, PlayerHurt, BulletNearMiss }
    public readonly struct SoundEvent {
        public SoundCue Cue { get; }
        public Vector3 Position { get; }
        public SoundEvent(SoundCue cue, Vector3 position) { Cue = cue; Position = position; }
    }
    // Gameplay broadcasts facts; the audio listener owns clips, mixing and voice limits.
    public static class SoundEvents {
        public static event Action<SoundEvent> Raised;
        public static void Emit(SoundCue cue, Vector3 position) { Raised?.Invoke(new SoundEvent(cue, position)); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Raised = null; }
    }
}
