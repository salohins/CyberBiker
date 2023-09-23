using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gameplay;

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

    [Header("Do not touch :)")]
    [SerializeField] CameraController camera;
    [SerializeField] ParticleSystem wheelParticle;

    
    [SerializeField] GameObject ragDoll;    
    [SerializeField] public Transform ragDollTarget;
    
    [SerializeField] private TMP_Text healthBarValue;
    [SerializeField] private Material healthBarMaterial;    

    [SerializeField] private GameObject helmetLight;
    [SerializeField] private Transform motorcycleMesh;
    [SerializeField] private GameObject playerMesh;    

    [HideInInspector] public float xVelocity, zVelocity;
    [HideInInspector] public float xThrow;

    private float yawRotation;
    private float animationFrame;    
    private float maxSpeed;    
    private float health;
    
    private int wallDirection;

    private Rigidbody rb;
    private Animator animator;
    private TouchInputManager tim;
    private GameplayManager gm;
    private TeleportController tc;

    private bool grounded;    

    private GameObject hitCar;

        
    void Start() {
        health = 100f;
        rb = GetComponent<Rigidbody>();
        tc = GetComponent<TeleportController>();
        animator = GetComponent<Animator>();
        tim = FindFirstObjectByType<TouchInputManager>();
        gm = FindFirstObjectByType<GameplayManager>();

        healthBarMaterial.SetFloat("_Cutoff", 0);

        maxSpeed = startMaxSpeed;

        SwitchRagDoll(false);
    }

    private void FixedUpdate() {
        if (gm.gameState == GameState.Playing)
            processVelocity();
        
        grounded = Physics.CheckSphere(transform.position - transform.up * 0.2f, 0.1f);

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
  
        if (Physics.CheckSphere(ragDollTarget.transform.position + transform.right * transform.GetComponent<BoxCollider>().bounds.size.x/2, 1f, LayerMask.GetMask("WorldObject"))) {
            wallDirection = 1;
        } else if (Physics.CheckSphere(ragDollTarget.transform.position - transform.right * transform.GetComponent<BoxCollider>().bounds.size.x/2, 1f, LayerMask.GetMask("WorldObject"))) {
            wallDirection = -1;
        }
        else {
            wallDirection = 0;
        }        

        if (gm.gameState == GameState.Playing)
            xThrow = tim.drag.x == 0 ? 0 : Mathf.Sign(tim.drag.x) == wallDirection ? 0 : tim.drag.x;
        else
            xThrow = 0;


    }

    private void Update() {

        ProcessYaw(); //Rotation on Y axis

        ProcessPitch(); //Rotation on X axis


        ProcessLeans();
        ProcessAnimations();
        ProcessParticleEffects();
        

        healthBarValue.text = health.ToString() + '%';

        if (health <= 0)
            gm.SwitchGameState(GameState.GameOver);

        if (gm.gameState == GameState.GameOver)
            maxSpeed = 0;
    }

    private void processVelocity() {
        rb.maxLinearVelocity = maxSpeed;

        xVelocity = transform.InverseTransformDirection(rb.velocity).x;
        zVelocity = transform.InverseTransformDirection(rb.velocity).z;
               

        //Limit max dodge speed

        if (Mathf.Abs(xVelocity) > dodgeMaxSpeed) { 
                //rb.velocity = new Vector3(Mathf.Sign(xVelocity) * dodgeMaxSpeed, rb.velocity.y, rb.velocity.z);
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

        // Stop motorcycle if no control input
        if (Mathf.Abs(rb.velocity.x) != 0 && tim.drag.x == 0) {
            rb.velocity = new Vector3(Mathf.Lerp(rb.velocity.x, 0, Time.deltaTime), rb.velocity.y, rb.velocity.z);
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

    private void ProcessAnimations() {
        animationFrame = Mathf.Lerp(animationFrame, xThrow, Time.deltaTime * 5f);
        animator.SetFloat("dragX", animationFrame);
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

    private void ProcessParticleEffects() {
      
    }

    private void GetDamage(int amount) {
        health -= amount;
        healthBarMaterial.SetFloat("_Cutoff", healthBarMaterial.GetFloat("_Cutoff") + (amount * 0.39f / 100f));
    }

    public void ResetRagDoll() {
        playerMesh.SetActive(true);
        if (hitCar.transform.tag != "Wall" && hitCar != null) {
            Destroy(hitCar);
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
        }
        else {
            transform.position = new Vector3(hitCar.transform.parent.transform.position.x, hitCar.transform.parent.transform.position.y, hitCar.transform.parent.transform.position.z + 10f);
        }
        tc.startFade(false);        
        SwitchRagDoll(false);
    }

    private void SwitchRagDoll(bool on) {
        

        foreach (Rigidbody ragRB in ragDoll.GetComponentsInChildren<Rigidbody>()) {
            ragRB.isKinematic = !on;
            ragRB.detectCollisions = on;
            ragRB.maxLinearVelocity = on ? 60 : 0;
            if (on)
                ragRB.GetComponent<Rigidbody>().AddForce((Vector3.up / 1.5f + transform.forward) * 50f, ForceMode.VelocityChange);
        }

        foreach (SkinnedMeshRenderer smr in ragDoll.GetComponentsInChildren<SkinnedMeshRenderer>()) {
            smr.enabled = on;
        }

        ragDoll.GetComponent<Animator>().enabled = !on;
        ragDoll.transform.SetParent(on ? null : motorcycleMesh.transform);

        if (!on) {
            ragDoll.transform.position = motorcycleMesh.transform.position;
            ragDoll.transform.SetPositionAndRotation(motorcycleMesh.transform.position, motorcycleMesh.transform.rotation);
        }

        

    }

    public void turnHelmetLight(bool on) {
        helmetLight.SetActive(on);
    }
        
    private float GetInspectorRotation(float angle) {
        if (angle > 180) {
            angle -= 360;
        }
        return angle;
    }

    

    private void OnCollisionEnter(Collision collision) {
        if (collision.transform.tag == "Obstacle" && gm.gameState == GameState.Playing) {
            gm.gameState = GameState.Hit;            
            tc.startFade(true);       
            hitCar = collision.gameObject;

            SwitchRagDoll(true);

            turnHelmetLight(false);

            playerMesh.SetActive(false);

            collision.gameObject.GetComponent<Car>().speed = 0;

            GetDamage(collision.gameObject.GetComponent<Obstacle>().damage);
        }

        if (collision.transform.tag == "Wall" && gm.gameState == GameState.Playing && wallDirection == 0) {
            gm.gameState = GameState.Hit;
            tc.startFade(true);
            hitCar = collision.gameObject;

            SwitchRagDoll(true);

            turnHelmetLight(false);

            playerMesh.SetActive(false);            

            //GetDamage(collision.gameObject.GetComponent<Obstacle>().damage);
        }
    }
}
