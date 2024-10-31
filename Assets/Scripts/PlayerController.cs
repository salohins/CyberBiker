using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gameplay;
using UnityEngine.Animations.Rigging;
using UnityEditor;

public class PlayerController : MonoBehaviour
{
    [Tooltip("Force that pushes motorcycle the bigger the value the faster motorcycle will accelerate. Be careful if the value is too low motorcycle will not reach max speed")]
    [SerializeField] private float accelerationForce;
    [Tooltip("Motorcycle speed at the start")]
    [SerializeField] private float startMaxSpeed;
    [Tooltip("Motorcycle rotation sensitivity on Y axis")]
    [SerializeField] private float rotationSensitivity;
    [Tooltip("Force power that pushes motorcycle sideways")]
    [SerializeField] private float dodgeAccelerationForce;
    [SerializeField] private float dodgeMaxSpeed;
    [Tooltip("Maximum angle for motorcycle lean animation (it's just visual thing and does not affect the gameplay)")]
    [SerializeField] private float leanRange;
    [SerializeField] private float shotInterval = .2f;

    [Header("Do not touch")]

    public Transform[] rayCheck;
    public GameObject cameraTarget;

    [HideInInspector] public float xVelocity, zVelocity;
    [HideInInspector] public float xThrow;
    
    [SerializeField] CameraController camera;    
    [SerializeField] GameObject ragDoll;
    [SerializeField] GameObject muzzleFlash;
    [SerializeField] private GameObject bullet;

    [SerializeField] private TMP_Text healthBarValue;
    [SerializeField] private Material healthBarMaterial;            
    [SerializeField] private GameObject playerMesh;
    [SerializeField] private ParticleSystem collectibleParticle;
    
    [SerializeField] private GameObject scope;
               
    private float yawRotation;
    private float animationFrame;    
    private float maxSpeed;    
    private float health;

    [HideInInspector] public bool grounded;

    private bool armed;
    public int wallDirection { get; private set; }

    private Rigidbody rb;
    private Animator animator;
    private TouchInputManager tim;
    private GameplayManager gm;
    private TeleportController tc;
    private RigBuilder animationRigging;
    private Transform motorcycleMesh;

    private GameObject hitObject;
    private GameObject _ragDoll;

    private bool isShooting;
    public bool shoot = false;

    public void PlayCollectibleParticle() {
        collectibleParticle.Play();
    }    
        
    void Start() {
        health = 100f;
        rb = GetComponent<Rigidbody>();
        tc = GetComponent<TeleportController>();
        animator = GetComponent<Animator>();
        animationRigging = GetComponent<RigBuilder>();
        motorcycleMesh = transform.GetChild(0);
        tim = FindFirstObjectByType<TouchInputManager>();
        gm = FindFirstObjectByType<GameplayManager>();

        healthBarMaterial.SetFloat("_Cutoff", 0);

        maxSpeed = startMaxSpeed;

        SwitchRagDoll(false);
    }


    public void SwitchShoot() {
        shoot = false;        
    }

    private void FixedUpdate() {              
        if (gm.gameState == GameState.Playing)
            processVelocity();
        
        grounded = Physics.CheckSphere(transform.position - transform.up * 0.2f, 0.1f);

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);


        wallDirection = 0;
        for (int i = 0; i < rayCheck.Length; i++) {
            

            if (Physics.Raycast(new Ray(rayCheck[i].position, Vector3.right), 1.5f, LayerMask.GetMask("WorldObject"))) {
                wallDirection = 1;
            } else if (Physics.Raycast(new Ray(rayCheck[i].position, Vector3.left), 1.5f, LayerMask.GetMask("WorldObject"))) {
                wallDirection = -1;
            }
        }

        /*if (Physics.Raycast(new Ray(rayCheck[1].transform.position, Vector3.right), 1.5f, LayerMask.GetMask("WorldObject"))) {
            wallDirection = 1;
            
        } else if (Physics.Raycast(new Ray(rayCheck[1].transform.position, Vector3.left), 1.5f, LayerMask.GetMask("WorldObject"))) {
            wallDirection = -1;
            
        }
        else {
            wallDirection = 0;
        }  */

        if (gm.gameState == GameState.Playing)
            xThrow = tim.drag.x == 0 ? 0 : Mathf.Sign(tim.drag.x) == wallDirection ? 0 : tim.drag.x;
        else
            xThrow = 0;





        //if (tim.aim) {
        //    RaycastHit hit;

        //    if (Physics.Raycast(new Ray(camera.transform.position, camera.transform.forward), out hit)) {                          

        //       if (hit.transform.gameObject.GetComponent<Enemy>() != null) {

        //}
        //}
        //}

        
    }

    

    IEnumerator ShootRate() {
        isShooting = true;
                                
        RaycastHit hit;

        if (Physics.Raycast(new Ray(camera.transform.position, camera.transform.forward), out hit)) {
            Vector3 targetPosition = hit.point;

            Vector3 direction = targetPosition - muzzleFlash.transform.position;

            Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);
                  
            muzzleFlash.GetComponent<MuzzleFlash>().Spawn();
            Instantiate(bullet, muzzleFlash.transform.position, bodyTargetRotation);
            animator.SetTrigger("Shoot");
            
        } else {
            Vector3 targetPosition = camera.transform.position + camera.transform.forward * 100f;

            Vector3 direction = targetPosition - muzzleFlash.transform.position;


            Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);

            Instantiate(bullet, muzzleFlash.transform.position, camera.transform.rotation);
        }        

        yield return new WaitForSeconds(shotInterval);

        isShooting = false;
    }

    private void Update() {    
        //Rotation of the character when aiming
        cameraTarget.transform.eulerAngles = new Vector3(0, camera.transform.eulerAngles.y + 10f, 0);

        scope.SetActive(animationRigging.layers[0].rig.weight > .2f && gm.gameState == GameState.Playing);
        healthBarValue.text = health.ToString() + '%';

        ProcessYaw(); //Rotation on Y axis
        ProcessPitch(); //Rotation on X axis        
        ProcessLeans();
        ProcessAnimations();        
                
        if (health <= 0)
            gm.SwitchGameState(GameState.GameOver);

        if (gm.gameState == GameState.GameOver)
            maxSpeed = 0;

        if (tim.aim && !isShooting && animationRigging.layers[0].rig.weight > .8f) {



            StartCoroutine(ShootRate());
            

            //muzzleFlash.GetComponent<MuzzleFlash>().Spawn();
            //animator.SetTrigger("Shoot");



            //hit.transform.GetComponent<Enemy>().GetDamage(20);


        }


    }

    private void TakeGun() => armed = true;

    private void processVelocity() {
        rb.maxLinearVelocity = gm.gameState == GameState.Hit ? 0 : maxSpeed;

        xVelocity = transform.InverseTransformDirection(rb.velocity).x;
        zVelocity = transform.InverseTransformDirection(rb.velocity).z;
               

        //Limit max dodge speed

        if (Mathf.Abs(rb.velocity.x) > dodgeMaxSpeed) { 
                rb.velocity = new Vector3(Mathf.Sign(rb.velocity.x) * dodgeMaxSpeed, rb.velocity.y, rb.velocity.z);
            }
            else {
                rb.AddForce((transform.right * (xThrow) * dodgeAccelerationForce), ForceMode.Acceleration);
            }
        

        if (grounded) {
            rb.AddForce(transform.forward * (accelerationForce), ForceMode.Acceleration);
        }
        else {
            rb.AddForce(Vector3.forward * (accelerationForce / 2), ForceMode.Acceleration);
        }

        if (wallDirection != 0)
            rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y, rb.maxLinearVelocity);
    }

    private void ProcessYaw() {
        yawRotation = Mathf.Lerp(yawRotation, xThrow * rotationSensitivity, Time.deltaTime * 3f);

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, yawRotation, transform.eulerAngles.z);
    }

    private void ProcessPitch() {
        float currentRotationX = GetInspectorRotation(transform.eulerAngles.x);
        float clampedRotationX = Mathf.Clamp(currentRotationX, -30, 10);


        Vector3 targetRotation = transform.eulerAngles;

        targetRotation.x = clampedRotationX;

        transform.eulerAngles = targetRotation;
    }

    public void Shoot() {
        muzzleFlash.GetComponent<MuzzleFlash>().Spawn();
        animator.SetTrigger("Shoot");

        RaycastHit hit;

        if (Physics.Raycast(new Ray(camera.transform.position, camera.transform.forward), out hit)) {
            if (hit.transform.gameObject.CompareTag("Enemy")) {                
                hit.transform.GetComponent<Enemy>().GetDamage(20);
            }
        }        
    }

    private void ProcessAnimations() {
        animationFrame = Mathf.Lerp(animationFrame, xThrow, Time.deltaTime * 3f);
        animator.SetFloat("dragX", animationFrame);        
        animator.SetBool("Aim", tim.aim);

        if (tim.aim && armed) {
            animationRigging.layers[0].rig.weight = Mathf.Lerp(animationRigging.layers[0].rig.weight, 1, Time.deltaTime * 3f);
            animationRigging.layers[1].rig.weight = 1;
            animator.SetLayerWeight(1, Mathf.Lerp(animator.GetLayerWeight(1), 1, Time.deltaTime));
        }
        else {
            animationRigging.layers[0].rig.weight = 0;
            animationRigging.layers[1].rig.weight = 0;
            animator.SetLayerWeight(1, 0);
            armed = false;
            animator.SetFloat("dragY", 0);
        }
    }

    private void ProcessLeans() {
        Vector3 targetRotation = Vector3.zero;

        targetRotation.z = leanRange * -xThrow;

        motorcycleMesh.transform.localRotation = Quaternion.Slerp(
            motorcycleMesh.transform.localRotation,
            Quaternion.Euler(targetRotation),
            Time.deltaTime * 3f
        );
    }

    private void GetDamage(int amount) {
        health -= amount;
        healthBarMaterial.SetFloat("_Cutoff", healthBarMaterial.GetFloat("_Cutoff") + (amount * 0.39f / 100f));
    }

    public void ResetRagDoll() {
        playerMesh.SetActive(true);
        
        if (hitObject != null) {
            if (hitObject.transform.tag != "Wall") {
                Destroy(hitObject);
                transform.position = new Vector3(0, transform.position.y, transform.position.z);
            }
            else {
                transform.position = new Vector3(hitObject.transform.parent.transform.position.x, hitObject.transform.parent.transform.position.y, hitObject.transform.parent.transform.position.z + 10f);
            }
        }
        
        camera.SetTarget(cameraTarget.transform);
        tc.startFade(false);        
        SwitchRagDoll(false);
    }
    
    private void SwitchRagDoll(bool on) {
        if (on) {
            _ragDoll = Instantiate(ragDoll, motorcycleMesh.position, motorcycleMesh.rotation);
            camera.SetTarget(_ragDoll.transform.GetChild(0).transform.GetChild(0));

            foreach (Rigidbody ragRB in _ragDoll.GetComponentsInChildren<Rigidbody>()) {                
                ragRB.GetComponent<Rigidbody>().AddForce((Vector3.up / 1.5f + transform.forward) * 50f, ForceMode.VelocityChange);
            }

        } else {
            Destroy(_ragDoll);
        }        
    }
        
    private float GetInspectorRotation(float angle) {
        if (angle > 180) {
            angle -= 360;
        }
        return angle;
    }    

    private void OnCollisionEnter(Collision collision) {        
        if (collision.transform.tag == "Obstacle" && gm.gameState == GameState.Playing) {

            Vector3 carBack = collision.transform.position - collision.transform.forward * (collision.collider.bounds.size.z/2);

            Vector3 toHit = (carBack - transform.position).normalized;

            if (transform.position.z + GetComponent<BoxCollider>().bounds.size.z/2 < carBack.z) {
                gm.gameState = GameState.Hit;
                tc.startFade(true);
                hitObject = collision.gameObject;

                SwitchRagDoll(true);


                playerMesh.SetActive(false);
                

                GetDamage(collision.gameObject.GetComponent<Obstacle>().damage);
            }            
        }

        if (collision.transform.tag == "Wall" && gm.gameState == GameState.Playing && wallDirection == 0) {
            gm.gameState = GameState.Hit;
            tc.startFade(true);
            hitObject = collision.gameObject;

            SwitchRagDoll(true);


            playerMesh.SetActive(false);                        
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.transform.CompareTag("Enemy")) {
            Instantiate(collectibleParticle, other.transform.position, Quaternion.identity, transform);
            GetDamage(10);
        }
    }

    private void OnParticleTrigger() {
        GetDamage(10);
    }

    private void OnParticleCollision(GameObject other) {
        Debug.Log("Im hit");
    }
}
