using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Vector3 targetOffset;            
    [SerializeField] private float smoothness = 3f;
    [Tooltip("Dynamic offset on x axis. Triggered by dragging the screeen left/right")]
    [SerializeField] private float xShiftOffset = -2;
    [Tooltip("Dynamic offset on Y axis. Triggered by dragging the screeen left/right. Works only one way (down)")]
    [SerializeField] private float yShiftOffset = 0.5f;
    [Tooltip("Dynamic Y axis rotation offset. Triggered by dragging the screeen left/right.")]
    [SerializeField] private float angleShiftOffset = -30f;

    private Transform target;
    private GameplayManager gm;
    private TouchInputManager tim;

    private Vector3 offset;

    private float velocity;
    private float rotationX, rotationY;

    private float sideOffset;
    private float rotationOffset;

    private PlayerController playerController;
    
    private void Start()
    {
        tim = FindFirstObjectByType<TouchInputManager>();
        gm = FindFirstObjectByType<GameplayManager>();
        playerController = FindFirstObjectByType<PlayerController>();
    }

    private void Update() {
        sideOffset = Mathf.Lerp(sideOffset, playerController.xThrow, Time.deltaTime);
    }

    private void LateUpdate() {
        Vector3 targetOffset;

        velocity = Mathf.Lerp(velocity, 5f, Time.deltaTime);
        
        if (!(gm.gameState == GameState.Hit)) {
            targetOffset = this.targetOffset;
            targetOffset -= transform.right * sideOffset * xShiftOffset;
            targetOffset += transform.up * Mathf.Abs(sideOffset) * yShiftOffset;

            rotationX = 0;
        } else {
            targetOffset = new Vector3(0, 0, this.targetOffset.z);

            rotationX = Mathf.Lerp(rotationX, 60f, Time.deltaTime);
        }               

        offset = Vector3.Lerp(
            offset,
            targetOffset,
            Time.deltaTime * smoothness
        );
        transform.position = target.position - transform.TransformDirection(offset);

        Vector3 targetRotation;
        Vector3 targetRotationShift = new Vector3(rotationX, rotationY, 0);

        rotationOffset = Mathf.Lerp(rotationOffset, playerController.xThrow * angleShiftOffset, Time.deltaTime);

        targetRotation = new Vector3(0, 0, rotationOffset);

        transform.rotation = (Quaternion.Lerp(transform.rotation, Quaternion.Euler(targetRotation + targetRotationShift), Time.deltaTime * 5f));        
    }

    public void SetTarget(Transform target) {
        this.target = target;
        offset = targetOffset;
        transform.position = target.position - transform.TransformDirection(offset);        
    }        
}
