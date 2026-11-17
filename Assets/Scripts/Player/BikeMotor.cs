using UnityEngine;

namespace CyberBiker.Player {
    public sealed class BikeMotor {
        private readonly Rigidbody body;
        private readonly Transform bike;
        private float acceleration;
        private float dodgeAcceleration;
        private float dodgeSpeed;
        public void Configure(float forwardAcceleration, float sidewaysAcceleration, float sidewaysSpeed) {
            acceleration = forwardAcceleration;
            dodgeAcceleration = sidewaysAcceleration;
            dodgeSpeed = sidewaysSpeed;
        }
        public BikeMotor(Rigidbody body, float acceleration, float dodgeAcceleration, float dodgeSpeed) {
            this.body = body;
            bike = body.transform;
            this.acceleration = acceleration;
            this.dodgeAcceleration = dodgeAcceleration;
            this.dodgeSpeed = dodgeSpeed;
        }
        public Vector3 LocalVelocity => bike.InverseTransformDirection(body.linearVelocity);
        public void TickSteering(float steering,float accelerationMultiplier=1) {
            var velocity=body.linearVelocity;
            if(Mathf.Abs(velocity.x)>dodgeSpeed)
                body.linearVelocity=new Vector3(Mathf.Sign(velocity.x)*dodgeSpeed,velocity.y,velocity.z);
            else body.AddForce(bike.right*steering*dodgeAcceleration*Mathf.Clamp01(accelerationMultiplier),ForceMode.Acceleration);
        }
        public void Tick(float steering, bool grounded, int wallDirection, float maxSpeed, float accelerationMultiplier = 1, float steeringAccelerationMultiplier = 1, bool preserveImpactVelocity = false, bool assistedSteering = false) {
            body.maxLinearVelocity = Mathf.Max(0, maxSpeed);
            if(!assistedSteering)TickSteering(steering,steeringAccelerationMultiplier);
            else {
                // The gap assist owns only lateral alignment. Keep the existing
                // forward speed cap while allowing the lateral component beside it.
                body.maxLinearVelocity=Mathf.Sqrt(maxSpeed*maxSpeed+body.linearVelocity.x*body.linearVelocity.x);
            }
            accelerationMultiplier=Mathf.Clamp01(accelerationMultiplier);
            Vector3 drive=grounded ? bike.forward : Vector3.forward*.5f;
            if(assistedSteering)drive=grounded ? Vector3.forward : Vector3.forward*.5f;
            float force=acceleration*accelerationMultiplier;
            if(preserveImpactVelocity || assistedSteering) {
                // At top speed, excess throttle makes Rigidbody's total-speed cap
                // shrink the sideways kick every step. Only fill actual headroom.
                float headroom=Mathf.Max(0,maxSpeed-Vector3.Dot(body.linearVelocity,drive.normalized));
                force=Mathf.Min(force,headroom/Mathf.Max(.001f,Time.fixedDeltaTime));
            }
            body.AddForce(drive * force, ForceMode.Acceleration);
            if (wallDirection != 0 && accelerationMultiplier>=.999f && !preserveImpactVelocity && !assistedSteering)
                body.linearVelocity = new Vector3(body.linearVelocity.x, body.linearVelocity.y, maxSpeed);
        }
        public void Stop() { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
    }
}
