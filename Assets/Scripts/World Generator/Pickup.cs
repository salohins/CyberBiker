using UnityEngine;

public abstract class Pickup : MonoBehaviour {
    private bool consumed;
    protected virtual void OnTriggerEnter(Collider other) {
        if (consumed) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsAlive || !Apply(player)) return;
        consumed = true;
        OnCollected(player);
        Destroy(gameObject);
    }
    protected abstract bool Apply(PlayerController player);
    protected virtual void OnCollected(PlayerController player) {
        player.PlayCollectibleParticle();
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Pickup, player.transform.position);
    }
}
