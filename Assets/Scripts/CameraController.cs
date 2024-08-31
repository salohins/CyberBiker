using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;

public class CameraController : MonoBehaviour {
    [SerializeField] private Vector3 targetOffset;
    [SerializeField] private Vector3 aimOffset;
    [SerializeField] private float smoothness = 3f;
    [Tooltip("Dynamic offset on x axis. Triggered by dragging the screeen left/right")]
    [SerializeField] private float xShiftOffset = -2;
    [Tooltip("Dynamic offset on Y axis. Triggered by dragging the screeen left/right. Works only one way (down)")]
    [SerializeField] private float yShiftOffset = 0.5f;
    [Tooltip("Dynamic Y axis rotation offset. Triggered by dragging the screeen left/right.")]
    [SerializeField] private float angleShiftOffset = -30f;

    [Tooltip("Dynamic Y axis rotation offset. Triggered by dragging the screeen left/right.")]
    [SerializeField] private float cameraXangle = 0;
    [SerializeField] private float cameraYangle = 0;

    private Transform target;
    private GameplayManager gm;
    private TouchInputManager tim;
    private Animator animator;


    private Vector3 offset;

    private float fov = 50f;
    private float velocity;
    private float rotationX, rotationY;

    private float sideOffset;
    private float rotationOffset;

    private PlayerController playerController;

    private void Start() {
        tim = FindFirstObjectByType<TouchInputManager>();
        gm = FindFirstObjectByType<GameplayManager>();
        playerController = FindFirstObjectByType<PlayerController>();
        animator = GameObject.FindGameObjectWithTag("Player").GetComponent<Animator>();
    }

    private void Update() {
        sideOffset = Mathf.Lerp(sideOffset, playerController.xThrow, Time.deltaTime);
        ProcessRotation();

    }

    private void LateUpdate() {


        Vector3 targetOffset;

        velocity = Mathf.Lerp(velocity, 5f, Time.deltaTime);

        if (!(gm.gameState == GameState.Hit)) {
            if (tim.aim) {
                targetOffset = aimOffset;

                fov = Mathf.Lerp(fov, 50f, Time.deltaTime * 1.5f);
                GetComponent<Camera>().fieldOfView = fov;
                //frameX = Mathf.Lerp(frameX, rotationX, Time.deltaTime * 5f);
                //frameY = Mathf.Lerp(frameY, rotationY, Time.deltaTime * 5f);
                //aimTarget.transform.rotation = transform.rotation;

                //aimTarget.transform.rotation = transform.rotation;

                //animator.SetFloat("dragX", rotationY);

            }
            else {
                targetOffset = this.targetOffset;
                targetOffset -= transform.right * sideOffset * xShiftOffset;
                targetOffset += transform.up * Mathf.Abs(sideOffset) * yShiftOffset;
                targetOffset += transform.forward * Mathf.Abs(sideOffset) * 2;

                if (playerController.grounded) {
                    fov = Mathf.Lerp(fov, 50f, Time.deltaTime * 1.5f);
                    GetComponent<Camera>().fieldOfView = fov;
                }
                else {
                    fov = Mathf.Lerp(fov, 80f, Time.deltaTime * 1f);
                    GetComponent<Camera>().fieldOfView = fov;
                }

            }


        }
        else {
            targetOffset = new Vector3(0, 0, this.targetOffset.z - 2f);
            fov = Mathf.Lerp(fov, 60f, Time.deltaTime * 3f);
            GetComponent<Camera>().fieldOfView = fov;
        }



        offset = Vector3.Lerp(
            offset,
            targetOffset,
            Time.deltaTime * smoothness
        );
        transform.position = target.position - transform.TransformDirection(offset);


        //aimTarget.transform.position = transform.position + transform.forward * 30f;
        //aimTarget.transform.rotation = transform.rotation;



    }

    private void ProcessRotation() {

        if (!(gm.gameState == GameState.Hit)) {
            if (tim.aim) {
                rotationY += tim.delta.x * Time.deltaTime * 6f;
                rotationX -= tim.delta.y * Time.deltaTime * 6f;

                rotationY = 0;
                rotationX = 0;
                animator.SetFloat("dragY", rotationX);
                animator.SetFloat("CamAngle", rotationY);

            }
            else {
                rotationX = Mathf.Lerp(rotationX, 0, Time.deltaTime * 5f);
                rotationY = Mathf.Lerp(rotationY, 0, Time.deltaTime * 5f);
            }
        }
        else {
            rotationX = Mathf.Lerp(rotationX, 40f, Time.deltaTime);
            rotationY = 0;
        }
        Vector3 targetRotation;
        Vector3 targetRotationShift = new Vector3(rotationX, rotationY, 0);

         
        rotationOffset = Mathf.Lerp(rotationOffset, playerController.xThrow * (tim.aim ? 0 : angleShiftOffset), Time.deltaTime);

        targetRotation = new Vector3(cameraXangle, cameraYangle, rotationOffset);

        transform.rotation = Quaternion.Euler(targetRotation + targetRotationShift);
    }

    public void SetTarget(Transform target) {
        this.target = target;
        offset = targetOffset;
        transform.position = target.position - transform.TransformDirection(offset);
    }
}
