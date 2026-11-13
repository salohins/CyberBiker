using UnityEngine;
using CyberBiker.Pooling;

public abstract class Pickup : MonoBehaviour {
    private bool consumed;
    protected virtual void OnTriggerEnter(Collider other) {
        if (consumed) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsAlive || !Apply(player)) return;
        consumed = true;
        OnCollected(player);
        // A pooled tile retains its collected children for the next tile lease.
        if(GetComponentInParent<PooledInstance>()!=null)gameObject.SetActive(false);else Destroy(gameObject);
    }
    protected abstract bool Apply(PlayerController player);
    public void ResetForReuse(){consumed=false;}
    protected virtual void OnCollected(PlayerController player) {
        player.PlayCollectibleParticle();
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Pickup, player.transform.position);
    }
}
