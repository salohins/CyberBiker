using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay;
using AI;
using CyberBiker.Combat;

public class Car : MonoBehaviour, IDamageable
{
    private Health vitality;
    public bool IsAlive => vitality != null && vitality.IsAlive;

    [SerializeField] public float speed = 10f;
    [SerializeField] public int whichWall;

    [Header("Damage and Destruction")]
    [Tooltip("Starting health, applied when the car spawns. Legacy 0 values keep the existing 5 HP fallback.")]
    [SerializeField, Min(0)] private int health;
    [Tooltip("Whether this car can receive damage. Enabled by default to preserve existing cars.")]
    [SerializeField] private bool destructible = true;
    [Tooltip("Play the assigned explosion effect when health reaches zero. Does not add area damage.")]
    [SerializeField] private bool explodeOnDeath = true;
    [Tooltip("Explosion particle prefab. The car hides its mesh and collider on death even if no effect is assigned.")]
    [SerializeField] private ParticleSystem explosion;
    public float CurrentHealth => vitality != null ? vitality.Current : (health > 0 ? health : 5);


    private bool hit;

    private GameObject player;
    private GameplayManager gm;

    private AIController aiController;

    public int lineNumber;

    private void Awake() {

    }

    void Start()
    {
        vitality = new Health(health > 0 ? health : 5);
        vitality.Died += Die;
        transform.SetParent(null);
        player = GameObject.FindGameObjectWithTag("Player");
        gm = FindAnyObjectByType<GameplayManager>();
        aiController = FindAnyObjectByType<AIController>();
    }

    public void GetDamage() { TakeDamage(new DamageInfo(1)); }
    public void TakeDamage(DamageInfo damage) { if (destructible) vitality?.TakeDamage(damage); }
    private void Die() {
        CyberBiker.Audio.SoundEvents.Emit(explodeOnDeath ? CyberBiker.Audio.SoundCue.Explosion : CyberBiker.Audio.SoundCue.Impact, transform.position);

        if (explodeOnDeath && explosion != null) Instantiate(explosion, transform);
        GetComponent<BoxCollider>().enabled = false;
        GetComponent<MeshRenderer>().enabled = false;
    }
    private void OnDestroy() { if (vitality != null) vitality.Died -= Die; }

    public Vector3 GetFrontPosition() {
        return transform.position + transform.forward * (GetComponent<BoxCollider>().bounds.size.z / 2);
    }

    public Vector3 GetBackPosition() {
        return transform.position - transform.forward * (GetComponent<BoxCollider>().bounds.size.z / 2);
    }

    private void Update() {
        if (hit) {
            speed = Mathf.Lerp(speed, 0, Time.deltaTime * 10f);

        }
    }

    private void OnCollisionEnter(Collision collision) {

        if (collision.collider.GetComponentInParent<PlayerController>() != null) {
            hit = true;
            GetComponent<BoxCollider>().enabled = false;
        }
    }

    void FixedUpdate()
    {

        LayerMask wallMask = LayerMask.GetMask("WorldObject");

        RaycastHit hit;

        if (!Physics.Raycast(new Ray(transform.position, transform.right * whichWall), out hit, 100f, wallMask)) {

            return;
        }

        if (player.transform.position.y - 15f > transform.position.y)
            return;

        if (transform.position.z + GetComponent<BoxCollider>().bounds.size.z / 2 < player.transform.position.z - player.GetComponent<BoxCollider>().bounds.size.z / 2) {
            return;
        }

        if (!IsAlive)
            return;

        Vector3 perp = Vector3.Cross(hit.normal, Vector3.up);
        Vector3 targetDir = Vector3.Project(transform.forward, perp).normalized;

        RaycastHit hit2;

        if (Physics.Raycast(new Ray(transform.position, -hit.normal), out hit2, 100f, wallMask)) {
            Vector3 predictPos = transform.position + targetDir;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(predictPos - transform.position),
                5f * Time.deltaTime
            );

            Debug.DrawLine(transform.position, hit2.point);

            transform.position = Vector3.MoveTowards(transform.position, predictPos, speed * Time.deltaTime);
        }
    }
}
