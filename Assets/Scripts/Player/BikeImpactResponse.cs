using Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace CyberBiker.Player {
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BikeImpactResponse : MonoBehaviour {
        private BikeSwordCombat finisher;
        [SerializeField] private bool affectCars = true;
        [SerializeField] private bool affectBarriers = true;
        [Tooltip("Maximum extra speed away from a car, in world units per second.")]
        [SerializeField, Range(0,12)] private float maximumKickSpeed = 5;
        [SerializeField, Min(0)] private float minimumImpactSpeed = .75f;
        [SerializeField, Min(1)] private float fullImpactSpeed = 18;
        [SerializeField, Range(0,1)] private float lightHitStrength = .25f;
        [Tooltip("Prevents multiple body contacts from repeatedly kicking the bike.")]
        [SerializeField, Range(.05f,1)] private float impactCooldown = .35f;
        [Header("Kick recovery")]
        [InspectorName("Frontal Motor Recovery Time (s)")]
        [Tooltip("Seconds to restore forward acceleration after a frontal hit. Does not set steering recovery.")]
        [SerializeField, Range(0,.6f)] private float motorRecoveryDuration = .4f;
        [InspectorName("Kick Recovery Time (s)")]
        [Tooltip("Seconds to restore full steering against a kick. Lower values feel more responsive; zero removes the extra steering resistance. Steering away is always immediate.")]
        [SerializeField, Range(0,.3f)] private float steeringRecoveryDuration = .16f;
        [Header("Recoil lean and rider animation")]
        [FormerlySerializedAs("recoilPoseResponse")]
        [InspectorName("Kick Lean Animation Speed")]
        [Tooltip("Bike and rider lean response only during kick recovery, opposing sideways movement, and the blend back to steering. Higher is faster. Normal steering lean keeps its original speed.")]
        [SerializeField, Range(1,20)] private float leanAnimationSpeed = 6;
        [Tooltip("Sideways movement speed that produces full lean during a kick.")]
        [SerializeField, Min(.1f)] private float recoilLeanSpeed = 8;
        [Tooltip("Maximum kick-induced lean as a fraction of normal full steering lean.")]
        [SerializeField, Range(0,1)] private float recoilLeanLimit = .35f;
        [InspectorName("Return To Steering Time (s)")]
        [Tooltip("Seconds to blend the lean from sideways movement back to steering after the kick resolves. Lower values return to steering sooner.")]
        [SerializeField, Range(.02f,1.5f)] private float recoilPoseBlendOut = .3f;
        private Rigidbody body;
        private GameplayManager manager;
        private float nextImpactTime, recoveryRemaining, steeringRecoveryRemaining;
        private bool recoilPoseActive;
        private float recoilReturnProgress;
        public int KickCount { get; private set; }
        public Vector3 LastKickVelocity { get; private set; }
        public Collider LastHitCollider { get; private set; }
        public bool IsRecovering => recoveryRemaining>0 || steeringRecoveryRemaining>0;
        public float PresentationResponse => recoilPoseActive ? Mathf.Clamp(leanAnimationSpeed,1,20) : BikePresentation.DefaultPoseResponse;
        public float PresentationSteering(float steering,float dt) {
            if(!recoilPoseActive) return steering;
            // Lanes run across world X. Projecting onto the yawed bike's right
            // vector would incorrectly include its much larger forward speed.
            float lateral=body.linearVelocity.x;
            dt=Mathf.Max(0,dt);
            bool stillSliding=Mathf.Abs(lateral)>.2f && (Mathf.Abs(steering)<.1f || lateral*steering<0);
            float motionSteering=Mathf.Clamp(lateral/Mathf.Max(.1f,recoilLeanSpeed),-recoilLeanLimit,recoilLeanLimit);
            if(IsRecovering || stillSliding) recoilReturnProgress=0;
            else recoilReturnProgress=Mathf.Clamp01(recoilReturnProgress+dt/Mathf.Max(.02f,recoilPoseBlendOut));
            // Blend two current signals, never a captured old pose. When momentum
            // opposes input, use motion alone; when aligned, both have the same sign.
            float t=recoilReturnProgress;
            float eased=t*t*t*(t*(t*6-15)+10);
            if(t>=1) recoilPoseActive=false;
            return Mathf.Lerp(motionSteering,steering,eased);
        }
        private void StartRecoilPose() {
            recoilPoseActive=Mathf.Abs(LastKickVelocity.x)>.1f;
            recoilReturnProgress=0;
        }
        public float MotorAccelerationMultiplier {
            get {
                if (recoveryRemaining<=0 || motorRecoveryDuration<=0) return 1;
                return RecoveryMultiplier(recoveryRemaining,motorRecoveryDuration);
            }
        }
        private float RecoveryMultiplier(float remaining,float duration,float holdFraction=.4f) {
            if(remaining<=0 || duration<=0) return 1;
            float progress=1-Mathf.Clamp01(remaining/duration);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(holdFraction,1,progress));
        }
        public float SteeringAccelerationMultiplier(float steering) {
            // Keep steering away from the car immediate, but don't let held input
            // into it erase the separation kick on the very next physics step.
            return Vector3.Dot(transform.right*steering,LastKickVelocity)<0
                ? RecoveryMultiplier(steeringRecoveryRemaining,steeringRecoveryDuration) : 1;
        }
        private float ImpactStrength(float speed) => Mathf.Lerp(lightHitStrength,1,
            Mathf.InverseLerp(minimumImpactSpeed,Mathf.Max(minimumImpactSpeed+1,fullImpactSpeed),speed));
        public Vector3 ApplyRearCrashKick(Collider car,Vector3 backward,float closingSpeed) {
            if(!isActiveAndEnabled || !affectCars || body.isKinematic || maximumKickSpeed<=0 || Time.timeScale<=0) return Vector3.zero;
            Vector3 direction=Vector3.ProjectOnPlane(backward,Vector3.up).normalized;
            bool sameImpact=LastHitCollider==car && Time.time<nextImpactTime;
            Vector3 sideKick=sameImpact ? LastKickVelocity*.25f : Vector3.zero;
            LastKickVelocity=Vector3.ClampMagnitude(direction*maximumKickSpeed*ImpactStrength(closingSpeed)+sideKick,maximumKickSpeed);
            LastHitCollider=car;
            StartRecoilPose();
            body.linearVelocity=new Vector3(LastKickVelocity.x,body.linearVelocity.y,LastKickVelocity.z);
            recoveryRemaining=motorRecoveryDuration;
            steeringRecoveryRemaining=steeringRecoveryDuration;
            nextImpactTime=Time.time+impactCooldown;
            if(!sameImpact) KickCount++;
            finisher?.BurnLaunchEnergy();
            return LastKickVelocity;
        }

        private void Awake() {
            finisher=GetComponent<BikeSwordCombat>();
            body=GetComponent<Rigidbody>();
            manager=FindAnyObjectByType<GameplayManager>();
        }
        private void OnCollisionEnter(Collision collision) { ReactToCollision(collision); }
        public void ReactToCollision(Collision collision) {
            if(finisher!=null && finisher.IgnoresTraffic(collision.collider))return;
            if (!isActiveAndEnabled || body.isKinematic || Time.timeScale<=0 || maximumKickSpeed<=0 || Time.time<nextImpactTime) return;
            if (manager!=null && manager.gameState!=GameState.Playing && manager.gameState!=GameState.Hit) return;
            float speed=0;
            Vector3 direction=Vector3.zero;
            Collider hitCollider=null;
            for (int i=0;i<collision.contactCount;i++) {
                var contact=collision.GetContact(i);
                bool car=ImpactSurfaces.IsCar(contact.otherCollider);
                if(car ? !affectCars : !affectBarriers || !ImpactSurfaces.IsBarrier(contact.otherCollider,contact.normal)) continue;
                Vector3 normal=contact.normal;
                if (Vector3.Dot(normal,body.worldCenterOfMass-contact.point)<0) normal=-normal;
                Vector3 horizontal=Vector3.ProjectOnPlane(normal,Vector3.up);
                if (horizontal.sqrMagnitude<.25f) continue;
                float closingSpeed=Mathf.Abs(Vector3.Dot(collision.relativeVelocity,normal));
                if (closingSpeed>speed) {speed=closingSpeed;direction=horizontal.normalized;hitCollider=contact.otherCollider;}
            }
            if (speed<minimumImpactSpeed || direction.sqrMagnitude<.5f) return;
            float strength=ImpactStrength(speed);
            LastKickVelocity=direction*(maximumKickSpeed*strength);
            // Apply at the centre of mass: separation without launching or tipping the bike.
            body.linearVelocity+=LastKickVelocity;
            LastHitCollider=hitCollider;
            StartRecoilPose();
            float frontal=Mathf.Clamp01(-Vector3.Dot(direction,Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized));
            recoveryRemaining=Mathf.Max(recoveryRemaining,motorRecoveryDuration*frontal);
            steeringRecoveryRemaining=Mathf.Max(steeringRecoveryRemaining,steeringRecoveryDuration);
            nextImpactTime=Time.time+impactCooldown;
            KickCount++;
            finisher?.BurnLaunchEnergy();
        }
        private void FixedUpdate() {
            if (Time.timeScale<=0 || (manager!=null && manager.gameState==GameState.Pause)) return;
            recoveryRemaining=Mathf.Max(0,recoveryRemaining-Time.fixedDeltaTime);
            steeringRecoveryRemaining=Mathf.Max(0,steeringRecoveryRemaining-Time.fixedDeltaTime);
        }
        public void ResetResponse() {
            recoveryRemaining=steeringRecoveryRemaining=nextImpactTime=0;
            LastKickVelocity=Vector3.zero;LastHitCollider=null;
            recoilPoseActive=false;recoilReturnProgress=0;
        }
        private void OnDisable() {ResetResponse();}
    }
}
