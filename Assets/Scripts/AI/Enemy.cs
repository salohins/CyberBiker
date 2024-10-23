using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI;
using System.Linq;
using UnityEditor;
using UnityEngine.Animations.Rigging;
using UnityEngine.UIElements;

public enum EnemyState {
    Spawn,
    Attack,
    Dead,
}

public class Enemy : MonoBehaviour {
    [SerializeField] private Transform[] rayCheck;
    private EnemyState state;

    private GameObject player;
    private float speed;
    private int moveDirection;

    public float distanceController;
    private float _playerDistance;

    [SerializeField]
    private GameObject motoMesh;

    [SerializeField]
    private Transform bodyRotation;

    [SerializeField]
    private ParticleSystem gunParticle1;

    [SerializeField]
    private ParticleSystem gunParticle2;

    [SerializeField]
    private Transform leftBulletSpawn;

    [SerializeField]
    private GameObject bullet;

    [SerializeField]
    public GameObject bulletParent;

    [SerializeField]
    private Transform rightBulletSpawn;

    [SerializeField]
    float health = 100f;

    [SerializeField]
    ParticleSystem damageParticle;

    private Animator animator;
    private RigBuilder animationRigging;

    private bool armed;

    [SerializeField] ParticleSystem explosion;

    public float playerDistance {
        get {
            return _playerDistance;
        }
        set {
            if (Vector3.Distance(transform.position, player.transform.position) < value) {
                moveDirection = 1;
            }
            else if (Vector3.Distance(transform.position, player.transform.position) > value) {
                moveDirection = -1;
            }
            else {
                moveDirection = 0;
            }
            _playerDistance = value;
        }
    }

    private AIController aiConntroller;
    private int currentWaypointIndex = 1;

    Vector3 followTarget;

    DodgePoint path;

    void Start() {
        player = GameObject.FindGameObjectWithTag("Player");
        _playerDistance = distanceController;
        aiConntroller = FindFirstObjectByType<AIController>();

        animator = GetComponent<Animator>();
        animationRigging = GetComponent<RigBuilder>();   
        if (bulletParent == null) {
            bulletParent = GameObject.FindGameObjectWithTag("Finish");
        }
    }

    private void GunShoot1() {
        Vector3 targetPosition = player.transform.position + Vector3.up * 3;
       
        Vector3 direction = targetPosition - rightBulletSpawn.transform.position;


        Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);

        Instantiate(bullet, rightBulletSpawn.transform.position, bodyTargetRotation, bulletParent.transform);
        //gunParticle1.Play();
    }

    private void GunShoot2() {
        Vector3 targetPosition = player.transform.position + Vector3.up * 3;
        
        Vector3 direction = targetPosition - leftBulletSpawn.transform.position;


        Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);


        Instantiate(bullet, leftBulletSpawn.transform.position, bodyTargetRotation, bulletParent.transform);
        //gunParticle2.Play();
    }

    private void Update() {
        // Handle the enemy's body rotation based on movement or look target
        var targetRotation = transform.eulerAngles;
        targetRotation.z = -targetRotation.y * 2f;
        targetRotation.x = 0;
        targetRotation.y = 0;

        motoMesh.transform.localRotation = Quaternion.Slerp(
            motoMesh.transform.localRotation,
            Quaternion.Euler(targetRotation),
            Time.deltaTime * 10f
        );

        if (followTarget != null && followTarget != Vector3.zero) {
            transform.LookAt(followTarget, Vector3.up);
        }
        else {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 10f);
        }

        // Shooting logic when the enemy is armed
        if (armed) {
            // Gradually blend the arm layers into the animation
            animationRigging.layers[1].rig.weight = 1;
            animationRigging.layers[0].rig.weight = Mathf.Lerp(animationRigging.layers[0].rig.weight, 1, Time.deltaTime * 5f);

            if (animationRigging.layers[0].rig.weight >= 0.9f) {
                animator.SetBool("GunReady", true);
                if (!isShootingRunning)
                    Shoot(); // Trigger the shooting sequence
            }

            // Rotate the body towards the player when armed
            Vector3 targetPosition = player.transform.position;
            Debug.DrawLine(transform.position + Vector3.up * 2, targetPosition + Vector3.up * 2);
            Vector3 direction = targetPosition - transform.position;

            
            Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);
            bodyRotation.transform.rotation = Quaternion.RotateTowards(bodyRotation.transform.rotation, bodyTargetRotation, Time.deltaTime * 120f);
            
            gunParticle2.transform.LookAt(player.transform.position + Vector3.up * 3f);
            gunParticle1.transform.LookAt(player.transform.position + Vector3.up * 3f);
        }
    }

    public float timeBetweenShots = .5f;
    private bool isShootingRunning;

    private void Shoot() {
        StartCoroutine(ShootArmsWithDelay());
    }

    private IEnumerator ShootArmsWithDelay() {
        isShootingRunning = true;
        // First shot with the left arm
        animator.SetLayerWeight(2, 1);
        animator.SetLayerWeight(1, 1);
        GunShoot1();

        animator.SetTrigger("LeftArmShoot");
         // Play particle effect for left arm

        yield return new WaitForSeconds(.5f);

        // Second shot with the right arm

        GunShoot2();
        animator.SetTrigger("RightArmShoot");
         // Play particle effect for right arm

        yield return new WaitForSeconds(.5f);

        // Reset the layers back to prevent overriding other animations
        animator.SetLayerWeight(2, 0);
        animator.SetLayerWeight(1, 0);
        isShootingRunning = false;
    }

    private void TakeGun() {
        armed = true;
    }

    private void FixedUpdate() {
        if (state == EnemyState.Dead)
            return;

        // Move the enemy based on the player's position and other conditions
        transform.Translate(Vector3.forward * Time.deltaTime * speed);

        if (Vector3.Distance(transform.position, player.transform.position) < 70) {
            state = EnemyState.Attack;
            moveDirection = 1;
        }

        if (state == EnemyState.Attack) {
            animator.SetBool("Attack", true);
        }

        if (state == EnemyState.Spawn) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 30f, Time.deltaTime);
        }
        else {
            if (player.GetComponent<PlayerController>().wallDirection != 0) {
                speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 2f, Time.deltaTime);
            }
            else {
                ProcessSpeed();
            }
        }

        if (path == null) {
            followTarget = new Vector3(rayCheck[1].position.x, transform.position.y, rayCheck[1].position.z) + transform.forward * 10f;
            path = aiConntroller.GetPath(new DodgePoint(transform.position, gameObject));
            currentWaypointIndex = 1;
        }

        if (path != null) {
            Vector3 currentWaypoint = new Vector3(path.GetPosition().x, transform.position.y, path.GetPosition().z);
            float step = speed * Time.deltaTime;
            followTarget = Vector3.MoveTowards(followTarget, currentWaypoint, step);

            if (followTarget.z >= currentWaypoint.z) {
                currentWaypointIndex++;
            }

            if (followTarget.z >= path.GetPosition().z) {
                ClearPath();
            }
        }
    }

    private void ClearPath() {
        path = null;
        currentWaypointIndex = 1;
        followTarget = Vector3.zero;
    }

    private void ProcessSpeed() {
        if (moveDirection == 1 && (Vector3.Distance(transform.position, player.transform.position) < _playerDistance)) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z + 10f, Time.deltaTime * 5f);
        }
        else if (moveDirection == -1 && (Vector3.Distance(transform.position, player.transform.position) > _playerDistance)) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 10f, Time.deltaTime * 5f);
        }
        else {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z, Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected() {
        if (path != null) {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path.GetPosition(), 1);
            Gizmos.DrawLine(transform.position, path.GetPosition());
        }

        if (followTarget != null && followTarget != Vector3.zero) {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(followTarget, 0.5f);
        }
    }

    private void OnParticleTrigger() {
        Debug.Log(2);

        int numParticles = gunParticle1.particleCount;
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[numParticles];
        int particleCount = gunParticle1.GetParticles(particles);

        for (int i = 0; i < particleCount; i++) {
            // Logic for handling each particle when it triggers
            Debug.Log("A particle triggered the collider!");
        }
    }

    private GameObject bulletHit;

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Enemy")) {
            bulletHit = other.gameObject;
            GetDamage(10);
            Instantiate(damageParticle, transform.position + Vector3.up * 2, Quaternion.identity, transform);
        }
        
    }

    private void OnTriggerStay(Collider other) {
        if (other.CompareTag("Enemy")) {
            if (other.gameObject == bulletHit) {
                Destroy(other.gameObject);

            }
            else {
                GetDamage(10);
                bulletHit = other.gameObject;
            }
        }
    }

    private void OnCollisionEnter(Collision collision) {
        GetDamage(50);
    }

    public void GetDamage(int amount) {
        health -= amount;
        Debug.Log("Enemy Health: " + health);
        if (health <= 0) {
            Instantiate(explosion, transform.position, Quaternion.identity);
            Destroy(gameObject);
            //Destroy(gameObject);
            
            
        }
    }

    private void OnParticleCollision(GameObject other) {
        Debug.Log(1);
        GetDamage(10);
    }
}
