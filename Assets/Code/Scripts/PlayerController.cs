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
    [SerializeField] private TMP_Text healthBarValue;
    private float health;

    [SerializeField] private Material healthBarMaterial;

    [SerializeField] private Transform motorcycleMesh;

    public float xVelocity, zVelocity;

    private float animationFrame;    

    private float xSpeed, zSpeed;
    private float maxSpeed;

    private Rigidbody rb;
    private Animator animator;
    private TouchInputManager tim;
    private GameplayManager gm;

    private bool grounded;
    

    Vector3 yawRotation = Vector3.zero;

    
    void Start() {
        health = 100f;
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        tim = FindObjectOfType<TouchInputManager>();
        gm = FindObjectOfType<GameplayManager>();

        healthBarMaterial.SetFloat("_Cutoff", 0);

        maxSpeed = startMaxSpeed;
    }


    private void FixedUpdate() {
        processVelocity();
        
        rb.maxLinearVelocity = maxSpeed;

        if (Mathf.Abs(rb.velocity.x) > dodgeMaxSpeed)
            rb.velocity = new Vector3(Mathf.Sign(rb.velocity.x) * dodgeMaxSpeed, rb.velocity.y, rb.velocity.z);        

        if (Physics.CheckSphere(transform.position - transform.up * 0.2f, 0.1f)) {
            grounded = true;
        } else {
            grounded = false;
        }

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
    }

    void Update()
    {
        processYaw();
        processLeans();
        processAnimations();
        ProcessPitch();        

        healthBarValue.text = health.ToString() + '%';        

        if (gm.gameState == GameState.GameOver)
            maxSpeed = 0;

        if (health <= 0) {
            gm.SwitchGameState(GameState.GameOver);
        }        
    }

    private void processYaw() {        
        yawRotation = Vector3.up * tim.drag.x;

        transform.eulerAngles += yawRotation * Time.deltaTime * rotationSensitivity;
    }

    private float InspectorRotation(float angle) {
        if (angle > 180) {
            angle -= 360;
        }
        return angle;
    }

    private void ProcessPitch() {
        float currentRotationX = InspectorRotation(transform.eulerAngles.x);
        float clampedRotationX = Mathf.Clamp(currentRotationX, -30, 10);


        Vector3 targetRotation = transform.eulerAngles;

        targetRotation.x = clampedRotationX;

        transform.eulerAngles = targetRotation;
    }



    private void processAnimations() {
        animationFrame = Mathf.Lerp(animationFrame, tim.drag.x, Time.deltaTime * 5f);

        animator.SetFloat("dragX", animationFrame);
    }

    private void processVelocity() {
        xVelocity = transform.InverseTransformDirection(rb.velocity).x;
        zVelocity = transform.InverseTransformDirection(rb.velocity).z;


        if (Mathf.Abs(rb.velocity.x) > dodgeMaxSpeed)
            rb.velocity = new Vector3(Mathf.Sign(rb.velocity.x) * dodgeMaxSpeed, rb.velocity.y, rb.velocity.z);
        else {
            rb.AddForce(transform.right * tim.drag.x * dodgeAccelerationForce, ForceMode.Acceleration);
        }

        

        if (grounded) {
            rb.AddForce(transform.forward * (accelerationForce), ForceMode.Acceleration);        
        } else {
            rb.AddForce(Vector3.forward * (accelerationForce / 2), ForceMode.Acceleration);
        }
    }

    private void processLeans() {
        Vector3 targetRotation = Vector3.zero;

        targetRotation.z = leanRange * -tim.drag.x;

        motorcycleMesh.transform.localRotation = Quaternion.Lerp(
            motorcycleMesh.transform.localRotation,
            Quaternion.Euler(targetRotation),
            Time.deltaTime * 3f
        );
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.transform.tag == "Obstacle") {
            Destroy(collision.gameObject);
            getDamage(collision.gameObject.GetComponent<Obstacle>().damage);
        }        
    }

    private void getDamage(int amount) {
        health -= amount;
        healthBarMaterial.SetFloat("_Cutoff", healthBarMaterial.GetFloat("_Cutoff") + (amount * 0.39f / 100f));
    }
}
