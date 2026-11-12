using UnityEngine;

public class Collectible : Pickup {
    public float rotationSpeed = 50f;
    public ParticleSystem pickupEffect;
    public float destroyDelay = 0.5f;
    private void Update() { transform.Rotate(0, 0, rotationSpeed * Time.deltaTime); }
    // A collectible is valid at full health; its effect does not modify player health.
    protected override bool Apply(PlayerController player) => true;
}
