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
    [Tooltip("Force power that pushes motorcycle sideways")]
    [SerializeField] private float dodgeForce;
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

    // Start is called before the first frame update
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
    }

    void Update()
    {
        processLeans();
        processAnimations();

        healthBarValue.text = health.ToString() + '%';

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0, transform.eulerAngles.z);

        if (gm.gameState == GameState.GameOver)
            maxSpeed = 0;
        else 
            maxSpeed = startMaxSpeed + gm.difficulty * 2;

        if (health <= 0) {
            gm.SwitchGameState(GameState.GameOver);
        }
    }

    private void processAnimations() {
        animationFrame = Mathf.Lerp(animationFrame, tim.drag.x, Time.deltaTime * 3f);

        animator.SetFloat("dragX", animationFrame);
    }

    private void processVelocity() {
        xVelocity = transform.InverseTransformDirection(rb.velocity).x;
        zVelocity = transform.InverseTransformDirection(rb.velocity).z;

        Debug.Log(zVelocity);
        rb.AddForce(transform.forward * (accelerationForce), ForceMode.Acceleration);

        rb.AddForce(new Vector3(tim.drag.x * dodgeForce, 0, accelerationForce), ForceMode.Acceleration);
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
