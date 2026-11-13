using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace CyberBiker.Player {
    public sealed class BikePresentation {
        public const float DefaultPoseResponse = 3;
        private readonly Transform transform, motorcycleMesh;
        private readonly Animator animator;
        private readonly RigBuilder animationRigging;
        private readonly float rotationSensitivity, leanRange;
        private float yawRotation, animationFrame, steering, visualSteering, poseResponse, dt, aimDt;
        public float AimRigBlendTime { get; set; } = .08f;
        private bool aiming, armed;
        public BikePresentation(Transform bike, Transform mesh, Animator animator, RigBuilder rig, float sensitivity, float lean) {
            transform = bike; motorcycleMesh = mesh; this.animator = animator;
            animationRigging = rig; rotationSensitivity = sensitivity; leanRange = lean;
        }
        public void Tick(float steering, bool aiming, bool armed, float? movementSteering=null, float response=DefaultPoseResponse, float? deltaTime=null,float? aimDeltaTime=null) {
            this.steering = steering; this.aiming = aiming; this.armed = armed;
            visualSteering=Mathf.Clamp(movementSteering ?? steering,-1,1);
            poseResponse=Mathf.Clamp(response,1,20);dt=Mathf.Max(0,deltaTime ?? Time.deltaTime);
            aimDt=Mathf.Max(0,aimDeltaTime ?? dt);
            // Use the original deltaTime * 3 interpolation during ordinary steering.
            // For pure Z-axis roll this also matches the original motorcycle Slerp.
            // Sharing the signal keeps the bike and rider together during a kick.
            animationFrame=Mathf.Lerp(animationFrame,visualSteering,dt*poseResponse);
            ProcessYaw(); ProcessPitch(); ProcessLeans(); ProcessAnimations();
        }
    private void ProcessYaw() {
        yawRotation = Mathf.Lerp(yawRotation, steering * rotationSensitivity, dt * 3f);

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
        animator.SetFloat("dragX", animationFrame);
        animator.SetFloat("JacketSteering", aiming ? animationFrame : 0);
        animator.SetBool("Aim", aiming);

        if (aiming && armed) {
            animationRigging.layers[0].rig.weight = Mathf.MoveTowards(animationRigging.layers[0].rig.weight, 1, aimDt / Mathf.Max(.03f,AimRigBlendTime));
            animationRigging.layers[1].rig.weight = 1;
            animator.SetLayerWeight(1, Mathf.Lerp(animator.GetLayerWeight(1), 1, dt));
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

        targetRotation.z = leanRange * -animationFrame;
        motorcycleMesh.transform.localRotation = Quaternion.Euler(targetRotation);
    }
    private float GetInspectorRotation(float angle) {
        if (angle > 180) {
            angle -= 360;
        }
        return angle;
    }
    }
}
