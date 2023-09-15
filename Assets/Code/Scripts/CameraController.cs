using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private TouchInputManager tim;

    private Vector3 offset;

    private float velocity;    
    private float sideOffset;
    private float rotationOffset;
    
    private void Start()
    {
        tim = FindFirstObjectByType<TouchInputManager>();
    }

    private void Update() {
        sideOffset = Mathf.Lerp(sideOffset, tim.drag.x, Time.deltaTime);
    }

    private void LateUpdate() {
        Vector3 targetOffset;

        velocity = Mathf.Lerp(velocity, 5f, Time.deltaTime);

        targetOffset = this.targetOffset;
        targetOffset -= transform.right * sideOffset * xShiftOffset;
        targetOffset += transform.up * Mathf.Abs(sideOffset) * yShiftOffset;

        offset = Vector3.Lerp(
            offset,
            targetOffset,
            Time.deltaTime * smoothness
        );

        Vector3 targetRotation;

        rotationOffset = Mathf.Lerp(rotationOffset, tim.drag.x * angleShiftOffset, Time.deltaTime);

        targetRotation = new Vector3(0, target.eulerAngles.y, rotationOffset);

        transform.rotation = (Quaternion.Lerp(transform.rotation, Quaternion.Euler(targetRotation), Time.deltaTime * 5f));

        transform.position = target.position - transform.TransformDirection(offset);
    }

    public void SetTarget(Transform target) {
        this.target = target;
        offset = targetOffset;
        transform.position = target.position - transform.TransformDirection(offset);        
    }        
}
