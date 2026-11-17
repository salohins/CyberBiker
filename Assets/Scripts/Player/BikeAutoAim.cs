using UnityEngine;
using UnityEngine.Animations.Rigging;
using Gameplay;

namespace CyberBiker.Player {
    [DisallowMultipleComponent]
    public sealed class BikeAutoAim : MonoBehaviour {
        [Header("Enemy targeting")]
        [SerializeField] private bool autoAimEnabled = true;
        [SerializeField, Min(1)] private float targetRange = 120;
        [Tooltip("Maximum angle to either side of the road's forward direction.")]
        [SerializeField, Range(10,85)] private float targetHalfAngle = 70;
        [SerializeField, Range(5,70)] private float targetPitchLimit = 45;
        [SerializeField, Min(.02f)] private float searchInterval = .15f;
        [Header("Upper body tracking")]
        [Tooltip("Higher values track a moving enemy faster. Does not change motorcycle lean.")]
        [SerializeField, Range(1,30)] private float trackingSpeed = 12;
        [SerializeField, Range(0,1)] private float torsoTurnShare = .65f;
        [SerializeField, Range(0,1)] private float torsoPitchShare = .5f;
        [Tooltip("Extra chest opening when the camera is on the far side of the pistol arm. Zero disables it. The gun still tracks the enemy.")]
        [SerializeField, Range(0,30)] private float pistolExposureYaw = 18;
        [SerializeField, Range(.05f,.75f)] private float pistolExposureSmoothTime = .2f;
        [SerializeField, Range(1,15)] private float firingAlignment = 5;
        [Tooltip("The existing pistol/muzzle effect points along local +X.")]
        [SerializeField] private Vector3 muzzleForwardAxis = Vector3.right;
        [Header("Neutral forward look")]
        [SerializeField] private bool levelHeadWithoutTarget=true;
        [SerializeField,Range(1,20)] private float neutralLookResponse=8;
        [SerializeField,Range(0,60)] private float neutralLookPitchLimit=45;

        private Transform muzzle, shoulder, gun, grip, torso, head, motorcycle, torsoTarget, targetPoint;
        private GameplayManager gameplay;
        private PlayerController player;
        private BikeSwordCombat sword;
        private float neutralLookAmount;
        private bool hasHeadRenderPose;
        private Quaternion finalHeadRotation;
        private Rigidbody bikeBody;
        private Animator animator;
        private Quaternion uprightTorsoPose, finalTorsoRotation, finalArmRotation, finalGunRotation;
        private Vector3 finalGunPosition;
        private readonly RaycastHit[] sightHits = new RaycastHit[64];
        private float nextSearch, projectileSpeed, torsoYaw, torsoPitch;
        private float exposureYaw,exposureVelocity;
        private CameraController viewController;
        private Vector3 targetVelocity, previousTargetPosition, trackedDirection;
        private bool active, ready, initialized, hadTargetSample, hasTorsoPose, hasRenderPose;
        public Enemy Target { get; private set; }
        public bool Enabled => isActiveAndEnabled && autoAimEnabled && initialized;
        public Vector3 AimPoint { get; private set; }
        public Vector3 TargetViewPoint => Target!=null ? GetTargetPoint(Target) : AimPoint;
        public bool CanFire { get; private set; }
        public Transform TorsoTarget => torsoTarget;
        public Transform Shoulder => shoulder;
        public Transform Muzzle => muzzle;
        public Transform Pistol => gun;
        public bool IsWeaponReady => active && ready;
        public float PistolExposureYaw => exposureYaw;
        public Vector3 AimDirection=>trackedDirection;
        public Vector3 HeadLookDirection=>head!=null ? head.forward : Vector3.forward;

        public void Initialize(Transform muzzleTransform, GameObject projectile, RigBuilder builder) {
            if(initialized || muzzleTransform==null || builder==null) return;
            muzzle=muzzleTransform;bikeBody=GetComponent<Rigidbody>();animator=GetComponent<Animator>();
            player=GetComponent<PlayerController>();sword=GetComponent<BikeSwordCombat>();gameplay=FindAnyObjectByType<GameplayManager>();
            // Gameplay uses the animated muzzle even when the player is briefly
            // outside a camera's culling results.
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            motorcycle=transform.GetChild(0);
            viewController=Camera.main!=null ? Camera.main.GetComponent<CameraController>() : null;
            var bullet=projectile!=null ? projectile.GetComponent<Bullet>() : null;
            projectileSpeed=bullet!=null ? bullet.bulletSpeed : 30;
            foreach(var bone in GetComponentsInChildren<Transform>(true)) {
                if(bone.name=="DEF-upper_arm.R") shoulder=bone;
                if(bone.name=="Gun_Main") gun=bone;
                if(bone.name=="GunTarget") grip=bone;
                if(bone.name=="DEF-spine.001") torso=bone;
                if(bone.name=="DEF-spine.006") head=bone;
            }
            initialized=shoulder!=null && gun!=null && grip!=null && torso!=null;
            if(!initialized) return;
            // Keep the existing animator and both authored rigs intact. This
            // marker describes the aiming rotation; it is never a camera target.
            torsoTarget=new GameObject("TorsoAimTarget").transform;
            trackedDirection=Vector3.forward;
            Camera.onPreCull+=ApplyRenderPose;
        }

        public void Tick(bool aiming,bool weaponReady,float dt) {
            bool wasActive=active;
            active=Enabled && aiming;ready=weaponReady;CanFire=false;
            if(active && !wasActive)nextSearch=0;
            if(!active || !ready) {hasTorsoPose=false;hasRenderPose=false;exposureYaw=exposureVelocity=0;}
            if(!active) {
                ClearTarget();
                torsoYaw=torsoPitch=0;
                return;
            }
            if(dt<=0) return;
            if(Target!=null && !IsEligible(Target,out _)) ClearTarget();
            if(Target==null && Time.time>=nextSearch) AcquireTarget();
            Vector3 direction=Vector3.forward;
            if(Target!=null) {
                Vector3 point=GetTargetPoint(Target);
                if(hadTargetSample) {
                    Vector3 velocity=(Target.transform.position-previousTargetPosition)/dt;
                    targetVelocity=velocity.sqrMagnitude<250*250
                        ? Vector3.Lerp(targetVelocity,velocity,1-Mathf.Exp(-dt*8)) : Vector3.zero;
                }
                previousTargetPosition=Target.transform.position;hadTargetSample=true;
                AimPoint=PredictPoint(point,targetVelocity);
                direction=(AimPoint-muzzle.position).normalized;
            }
            float blend=1-Mathf.Exp(-dt*Mathf.Max(1,trackingSpeed));
            trackedDirection=Vector3.Slerp(trackedDirection,direction,blend).normalized;
            if(Target==null)AimPoint=muzzle.position+trackedDirection*120;
            float yaw=Mathf.Atan2(trackedDirection.x,trackedDirection.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Asin(Mathf.Clamp(trackedDirection.y,-1,1))*Mathf.Rad2Deg;
            // Keep the full-body aim pose on its authored neutral clip. The
            // jacket's separate steering control must not select full-body poses.
            animator.SetFloat("CamAngle",0);animator.SetFloat("dragY",0);
            torsoPitch=Mathf.LerpAngle(torsoPitch,ready ? pitch*torsoPitchShare : 0,blend);
            torsoYaw=Mathf.LerpAngle(torsoYaw,ready ? yaw*torsoTurnShare : 0,blend);
            float exposure=0;
            if(ready && viewController!=null) {
                Vector3 aimingRight=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(trackedDirection,Vector3.up).normalized);
                Vector3 viewPosition=transform.position+viewController.AimFollowOffset;
                float farSide=Vector3.Dot(viewPosition-torso.position,aimingRight);
                float openingDirection=Mathf.Clamp(yaw/12,-1,1);
                exposure=openingDirection*pistolExposureYaw*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,-2,farSide));
            }
            exposureYaw=Mathf.SmoothDampAngle(exposureYaw,exposure,ref exposureVelocity,Mathf.Max(.05f,pistolExposureSmoothTime),Mathf.Infinity,dt);
            torsoTarget.SetPositionAndRotation(torso.position,Quaternion.Euler(torsoPitch,torsoYaw,0));
        }

        public void ApplyArmAim() {
            CanFire=false;
            if(Time.deltaTime<=0)return;
            hasRenderPose=false;hasHeadRenderPose=false;
            if(!active || !ready){ApplyNeutralHeadLook();return;}
            if(!hasTorsoPose) {
                // Keep the complete authored aiming posture, including the
                // spine's bone-axis orientation. Only remove the motorcycle bank.
                uprightTorsoPose=Quaternion.Inverse(motorcycle.rotation)*torso.rotation;
                hasTorsoPose=true;
            }
            torso.rotation=Quaternion.Euler(torsoPitch,torsoYaw+exposureYaw,0)*uprightTorsoPose;
            gun.SetPositionAndRotation(grip.position,grip.rotation);
            // Keep the barrel aligned during the smooth return to road-forward
            // as well as while tracking an enemy. Camera pitch is framing only.
            Vector3 bore=muzzle.TransformDirection(muzzleForwardAxis).normalized;
            Quaternion correction=Quaternion.FromToRotation(bore,trackedDirection);
            correction=Quaternion.RotateTowards(Quaternion.identity,correction,70);
            shoulder.rotation=correction*shoulder.rotation;
            gun.SetPositionAndRotation(grip.position,grip.rotation);
            if(Target!=null && IsEligible(Target,out _)) {
                Vector3 desired=(AimPoint-muzzle.position).normalized;
                CanFire=Vector3.Angle(muzzle.TransformDirection(muzzleForwardAxis),desired)<=firingAlignment
                    && HasLineOfSight(AimPoint,Target);
            }
            finalTorsoRotation=torso.rotation;finalArmRotation=shoulder.rotation;
            finalGunPosition=gun.position;finalGunRotation=gun.rotation;hasRenderPose=true;
            ApplyNeutralHeadLook();
        }

        private void ApplyNeutralHeadLook(){
            if(!Enabled || head==null || player==null || gameplay==null)return;
            // The riding clip owns the relaxed helmet angle. This correction
            // belongs only to the drawn-pistol pose, never ordinary riding.
            if(!active || !ready){neutralLookAmount=0;return;}
            bool mounted=sword==null || sword.Phase==BikeSwordCombat.CombatPhase.Idle || sword.Phase==BikeSwordCombat.CombatPhase.Pursuing;
            bool neutral=levelHeadWithoutTarget && Target==null && mounted && player.IsAlive && gameplay.gameState==GameState.Playing;
            neutralLookAmount=Mathf.MoveTowards(neutralLookAmount,neutral ? 1 : 0,Time.deltaTime*Mathf.Max(1,neutralLookResponse));
            if(!mounted || !player.IsAlive || gameplay.gameState!=GameState.Playing || neutralLookAmount<=0)return;
            var forward=head.forward;var level=Vector3.ProjectOnPlane(forward,Vector3.up);
            if(level.sqrMagnitude<.001f)return;
            var correction=Quaternion.FromToRotation(forward,level.normalized);
            correction=Quaternion.RotateTowards(Quaternion.identity,correction,neutralLookPitchLimit);
            // Correct only the helmet's vertical gaze. Preserve the riding
            // crouch, steering bank, and the authored sword animation poses.
            head.rotation=Quaternion.Slerp(Quaternion.identity,correction,neutralLookAmount)*head.rotation;
            finalHeadRotation=head.rotation;hasHeadRenderPose=true;
        }

        private void ApplyRenderPose(Camera view) {
            if(!Enabled || view.cameraType!=CameraType.Game)return;
            // Apply absolute cached poses, so multiple cameras cannot accumulate
            // extra rotation or restore the animation's pre-tracking pose.
            if(hasRenderPose && active && ready){
                torso.rotation=finalTorsoRotation;shoulder.rotation=finalArmRotation;
                gun.SetPositionAndRotation(finalGunPosition,finalGunRotation);
            }
            if(hasHeadRenderPose && head!=null)head.rotation=finalHeadRotation;
        }
        private void AcquireTarget() {
            nextSearch=Time.time+Mathf.Max(.02f,searchInterval);
            Enemy best=null;float bestScore=float.PositiveInfinity;
            var enemies=Enemy.Instances;
            for(int enemyIndex=0;enemyIndex<enemies.Count;enemyIndex++) {
                var enemy=enemies[enemyIndex];
                if(!IsEligible(enemy,out var point)) continue;
                Vector3 offset=point-muzzle.position;
                float score=offset.sqrMagnitude*(1+Vector3.Angle(Vector3.forward,offset)/90);
                if(score<bestScore) {best=enemy;bestScore=score;}
            }
            var previousTarget=Target;Target=best;hadTargetSample=false;targetVelocity=Vector3.zero;
            if(Target!=previousTarget)targetPoint=null;
            if(Target!=null && targetPoint==null) {
                foreach(var bone in Target.GetComponentsInChildren<Transform>())
                    if(bone.name=="DEF-spine.003") {targetPoint=bone;break;}
            }
            if(Target!=null)previousTargetPosition=Target.transform.position;
        }
        private Vector3 GetTargetPoint(Enemy enemy) {
            if(enemy==Target && targetPoint!=null) return targetPoint.position;
            var collider=enemy.GetComponent<Collider>();
            return collider!=null && collider.enabled
                ? collider.bounds.center+Vector3.up*collider.bounds.size.y*.15f
                : enemy.transform.position+Vector3.up*1.5f;
        }
        private bool IsEligible(Enemy enemy,out Vector3 point) {
            point=Vector3.zero;
            if(enemy==null || !enemy.gameObject.activeInHierarchy || !enemy.IsAlive || enemy.IsEncounterQueued || enemy.IsArriving) return false;
            point=GetTargetPoint(enemy);Vector3 offset=point-muzzle.position;
            if(offset.sqrMagnitude>targetRange*targetRange || offset.z<=0) return false;
            float yaw=Mathf.Abs(Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg);
            float pitch=Mathf.Abs(Mathf.Atan2(offset.y,new Vector2(offset.x,offset.z).magnitude)*Mathf.Rad2Deg);
            return yaw<=targetHalfAngle && pitch<=targetPitchLimit && HasLineOfSight(point,enemy);
        }
        private bool HasLineOfSight(Vector3 point,Enemy enemy) {
            Vector3 delta=point-muzzle.position;float distance=delta.magnitude;
            if(distance<.001f) return true;
            int count=Physics.RaycastNonAlloc(muzzle.position,delta/distance,sightHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length) return false;
            for(int i=0;i<count;i++) {
                var hit=sightHits[i].collider;
                if(hit.transform.IsChildOf(transform) || hit.GetComponentInParent<Enemy>()==enemy || hit.GetComponentInParent<Bullet>()!=null) continue;
                return false;
            }
            return true;
        }
        private Vector3 PredictPoint(Vector3 point,Vector3 velocity) {
            Vector3 offset=point-muzzle.position,direction=offset.normalized;
            float travel=0;
            for(int i=0;i<3;i++) {
                float speed=projectileSpeed+(bikeBody!=null ? Mathf.Max(0,Vector3.Dot(bikeBody.linearVelocity,direction)) : 0);
                float a=Vector3.Dot(velocity,velocity)-speed*speed;
                float b=2*Vector3.Dot(offset,velocity),c=offset.sqrMagnitude;
                float discriminant=b*b-4*a*c;
                travel=0;
                if(Mathf.Abs(a)<.001f) {if(b<-.001f) travel=-c/b;}
                else if(discriminant>=0) {
                    float root=Mathf.Sqrt(discriminant),t1=(-b-root)/(2*a),t2=(-b+root)/(2*a);
                    if(t1>0 && t2>0) travel=Mathf.Min(t1,t2);else travel=Mathf.Max(0,Mathf.Max(t1,t2));
                }
                travel=Mathf.Min(travel,2);
                direction=(offset+velocity*travel).normalized;
            }
            return point+velocity*travel;
        }
        private void ClearTarget() {Target=null;targetPoint=null;hadTargetSample=false;targetVelocity=Vector3.zero;CanFire=false;}
        private void OnDisable() {active=false;hasRenderPose=hasHeadRenderPose=false;neutralLookAmount=0;ClearTarget();}
        private void OnDestroy() {Camera.onPreCull-=ApplyRenderPose;if(torsoTarget!=null) Destroy(torsoTarget.gameObject);}
    }
}
