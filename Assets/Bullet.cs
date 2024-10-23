using UnityEngine;

public class Bullet : MonoBehaviour {
    public float bulletSpeed = 30f; // Speed of the bullet
    private Rigidbody rb;
    [SerializeField] GameObject trailPrefab;

    

    void Start() {
        rb = GetComponent<Rigidbody>();
        //rb.velocity = transform.forward * bulletSpeed; // Move forward based on initial direction
        //trailPrefab = Instantiate(trailPrefab, transform.position, Quaternion.identity);
    }

    private void Update() {
        transform.position += transform.forward * Time.deltaTime * bulletSpeed;
        //trailPrefab.transform.position = transform.localPosition;
    }

    // Destroy the bullet when it goes out of bounds after 5 seconds (optional)
    private void OnBecameInvisible() {
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            Debug.Log("Bullet hit the player!");
            Destroy(gameObject); // Destroy the bullet after collision
        }
        else if (other.CompareTag("Enemy")) {
            Debug.Log("Bullet hit the enemy!");            
            Destroy(gameObject); // Destroy the bullet after collision
        }
        else {
            // Optional: Destroy bullet if it hits anything else
            Destroy(gameObject);
        }

    }

    private void OnCollisionEnter(Collision collision) {
        Destroy(gameObject);
    }
}
