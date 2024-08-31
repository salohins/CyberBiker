using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collectible : MonoBehaviour
{
    // Speed of the rotation in degrees per second
    public float rotationSpeed = 50f;

    public ParticleSystem pickupEffect;
    public float destroyDelay = 0.5f;

    private PlayerController player;

    void Update() {
        // Rotate the object around its Z axis at the specified speed
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        player = FindAnyObjectByType<PlayerController>();
    }

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {

            if (pickupEffect != null) {
                player.PlayCollectibleParticle();
            }

        }

        Destroy(gameObject);
    }
}
