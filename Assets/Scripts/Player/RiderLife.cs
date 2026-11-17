using Gameplay;
using UnityEngine;

namespace CyberBiker.Player {
    // Additive presentation after aim/IK, with explicit ownership boundaries for
    // weapons, sword poses, collision recovery and projectile dodges.
    [DefaultExecutionOrder(50),DisallowMultipleComponent]
    public sealed class RiderLife : MonoBehaviour {
        [SerializeField] private bool reactionsEnabled=true;
        [Header("Brief glances while riding")]
        [SerializeField,Range(5,35)] private float carNoticeRange=20;
        [SerializeField,Range(2,12)] private float carSideRange=7;
        [SerializeField,Range(10,80)] private float enemyNoticeRange=45;
        [SerializeField,Range(10,75)] private float headYawLimit=55;
        [SerializeField,Range(0,15)] private float ridingHeadPitchLimit=6;
        [SerializeField,Range(.2f,1.5f)] private float glanceDuration=.75f;
        [SerializeField,Range(.5f,5)] private float glanceCooldown=1.65f;
        [SerializeField,Range(.08f,.6f)] private float headResponse=.18f;
        [Header("Car anticipation and neck movement")]
        [SerializeField,Range(.3f,2)] private float carAnticipationTime=1.1f;
        [SerializeField,Range(.05f,.4f)] private float carLookResponse=.14f;
        [SerializeField,Range(.15f,.8f)] private float carReturnResponse=.4f;
        [SerializeField,Range(0,.65f)] private float carNeckShare=.4f;
        [SerializeField,Range(5,35)] private float carPitchRange=25;
        [Header("Body and wind response")]
        [SerializeField,Range(0,.15f)] private float bodyTurnShare=.055f;
        [SerializeField,Range(0,5)] private float passBodyLean=2.4f;
        [SerializeField,Range(.25f,1.2f)] private float passRecovery=.65f;
        [SerializeField,Range(0,1.5f)] private float breathingAmount=.4f;
        [SerializeField,Range(0,1)] private float idleSway=.3f;
        [Header("Head follows the selected enemy while aiming")]
        [SerializeField] private bool followAimTarget=true;
        [SerializeField,Range(5,35)] private float aimingHeadPitchLimit=22;
        [SerializeField,Range(.03f,.2f)] private float combatHeadResponse=.07f;
        [Header("Visible enemy firing at the player — highest attention priority")]
        [SerializeField,Range(30,150)] private float shootingEnemyRange=120;

        private PlayerController player;
        private GameplayManager manager;
        private BikeAutoAim aim;
        private BikeSwordCombat sword;
        private BikeImpactResponse impact;
        private PlayerProjectileEvade evade;
        private Animator animator;
        private Camera viewCamera;
        private Rigidbody bikeBody;
        private Transform chest,neck,head,attention;
        private Enemy combatAttention,previousAimTarget;
        private Vector3 attentionOffset,passPoint;
        private float clock,nextScan,nextGlance,glanceUntil,passAge=100,passStrength,passSide;
        private float yaw,pitch,yawVelocity,pitchVelocity,bodyYaw,bodyRoll,bodyPitch;
        private bool initialized,started,hasPose,chestApplied,headApplied,passGlance;
        private bool wasAiming,poseWasAiming;
        private bool attentionIsCar,carReturning,neckApplied;
        private float neckShare,neckVelocity;
        private float nextThreatScan;
        private Quaternion chestBase,chestFinal,headBase,headFinal;
        private Quaternion neckBase,neckFinal;
        private static readonly int AimParameter=Animator.StringToHash("Aim");

        public float HeadYaw=>yaw;
        public float HeadPitch=>pitch;
        public float BodyRoll=>bodyRoll;
        public bool HasAttention=>attention!=null || passGlance;
        public bool PoseApplied=>hasPose;
        public bool BodyPoseApplied=>chestApplied;
        public bool NeckPoseApplied=>neckApplied;
        public float NeckTurn=>neckApplied ? Quaternion.Angle(neckBase,neckFinal) : 0;
        public Enemy PriorityEnemy=>combatAttention;
        public int GlanceCount {get;private set;}
        public int PassReactionCount {get;private set;}

        private void Awake(){
            player=GetComponent<PlayerController>();aim=GetComponent<BikeAutoAim>();sword=GetComponent<BikeSwordCombat>();
            impact=GetComponent<BikeImpactResponse>();evade=GetComponent<PlayerProjectileEvade>();animator=GetComponent<Animator>();
            manager=FindAnyObjectByType<GameplayManager>();
            viewCamera=Camera.main;
            bikeBody=GetComponent<Rigidbody>();
            foreach(var bone in GetComponentsInChildren<Transform>(true)){
                if(bone.name=="DEF-spine.003")chest=bone;
                if(bone.name=="DEF-spine.005")neck=bone;
                if(bone.name=="DEF-spine.006")head=bone;
            }
            initialized=player!=null && manager!=null && animator!=null && head!=null && chest!=null;
        }
        private void Start(){started=true;Camera.onPreCull-=RenderPose;Camera.onPreCull+=RenderPose;}
        private void OnEnable(){if(started)Camera.onPreCull+=RenderPose;}
        private void OnDisable(){Camera.onPreCull-=RenderPose;RemovePose();Clear();}
        private bool Aiming=>(player!=null && player.IsPistolEngaged) || (animator!=null && animator.GetBool(AimParameter));
        private bool Mounted=>sword==null || sword.Phase==BikeSwordCombat.CombatPhase.Idle || sword.Phase==BikeSwordCombat.CombatPhase.Pursuing;
        private bool CanReact=>initialized && reactionsEnabled && player.IsAlive && Mounted &&
            (impact==null || !impact.IsRecovering) && (evade==null || evade.PoseAmount<.001f);

        private void Update()=>Tick(Time.deltaTime);
        private void Tick(float dt){
            if(!initialized)return;
            if(manager.gameState==GameState.Pause || dt<=0)return;
            RemovePose();
            if(!CanReact || manager.gameState!=GameState.Playing){Clear();return;}
            clock+=dt;passAge+=dt;
            // Threats interrupt an existing glance and ignore its cooldown.
            if(clock>=nextThreatScan){nextThreatScan=clock+.08f;combatAttention=FindCombatThreat();}
            if(!IsCombatThreat(combatAttention))combatAttention=null;
            if(Aiming){attention=null;attentionIsCar=false;passGlance=false;passAge=100;return;}
            if(IsCombatThreat(combatAttention)){
                if(attention!=combatAttention.transform)GlanceCount++;
                attention=combatAttention.transform;attentionOffset=Vector3.up*2.5f;attentionIsCar=false;passGlance=false;passAge=100;glanceUntil=clock+.2f;
                return;
            }
            combatAttention=null;
            float attentionRange=attentionIsCar ? 100 : enemyNoticeRange*1.5f;
            if(attention!=null && (!attention.gameObject.activeInHierarchy || (attention.position-transform.position).sqrMagnitude>attentionRange*attentionRange))attention=null;
            if(clock>=glanceUntil){attention=null;passGlance=false;}
            if(clock<nextScan || HasAttention)return;
            bool urgentOnly=clock<nextGlance;
            if(urgentOnly && clock<glanceUntil+.35f)return;
            nextScan=clock+.08f;FindAttention(urgentOnly);
        }

        private bool IsCombatThreat(Enemy enemy){
            if(enemy==null || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || enemy.IsEncounterQueued || enemy.IsArriving || enemy.IsFinisherReady)return false;
            var delta=enemy.transform.position-transform.position;
            if(delta.sqrMagnitude>shootingEnemyRange*shootingEnemyRange || !(enemy.IsAimingWeapons || Time.time-enemy.LastFiredTime<1.5f))return false;
            if(viewCamera==null)viewCamera=Camera.main;
            if(viewCamera==null)return false;
            var visible=viewCamera.WorldToViewportPoint(enemy.transform.position+Vector3.up*2.5f);
            return visible.z>viewCamera.nearClipPlane && visible.x>=0 && visible.x<=1 && visible.y>=0 && visible.y<=1;
        }
        private Enemy FindCombatThreat(){
            Enemy best=null;float score=float.PositiveInfinity;var enemies=Enemy.Instances;
            for(int i=0;i<enemies.Count;i++){
                var enemy=enemies[i];if(!IsCombatThreat(enemy))continue;
                float candidate=(enemy.transform.position-transform.position).sqrMagnitude;
                if(Time.time-enemy.LastFiredTime<1.5f)candidate-=100000;
                if(candidate<score){score=candidate;best=enemy;}
            }
            return best;
        }

        private void FindAttention(bool urgentCarsOnly=false){
            Transform best=null;Vector3 offset=Vector3.zero;float bestScore=float.PositiveInfinity,hold=glanceDuration;bool isCar=false;
            var origin=transform.position;
            var cars=Car.Instances;
            for(int i=0;i<cars.Count;i++){
                var car=cars[i];
                if(car==null || !car.IsAlive || !car.gameObject.activeInHierarchy)continue;
                var delta=car.transform.position-origin;
                float closing=Mathf.Max(0,(bikeBody!=null ? bikeBody.linearVelocity.z : 0)-(car.isActiveAndEnabled ? Mathf.Max(0,car.speed*car.transform.forward.z) : 0));
                float leadDistance=Mathf.Clamp(closing*carAnticipationTime,carNoticeRange,90);
                float timeToPass=closing>1 ? Mathf.Max(0,delta.z)/closing : float.PositiveInfinity;
                if(delta.z< -2 || delta.z>leadDistance || Mathf.Abs(delta.x)>carSideRange || Mathf.Abs(delta.x)<1 || Mathf.Abs(delta.y)>5)continue;
                if(urgentCarsOnly && (closing<8 || timeToPass>carAnticipationTime))continue;
                float score=Mathf.Abs(delta.z)*.45f+Mathf.Abs(delta.x);
                if(score<bestScore){
                    bestScore=score;best=car.transform;isCar=true;
                    offset=car.TryGetTrafficShape(0,out var bounds,out _) ? car.transform.InverseTransformPoint(bounds.center+Vector3.up*bounds.extents.y*.8f) : Vector3.up*1.5f;
                    hold=float.IsInfinity(timeToPass) ? glanceDuration : Mathf.Clamp(timeToPass+.2f,glanceDuration,2);
                }
            }
            var enemies=Enemy.Instances;
            for(int i=0;i<enemies.Count;i++){
                if(urgentCarsOnly)break;
                var enemy=enemies[i];
                if(enemy==null || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || enemy.IsEncounterQueued || enemy.ArrivalVisibility<.15f)continue;
                var delta=enemy.transform.position-origin;
                if(delta.z< -2 || delta.z>enemyNoticeRange || Mathf.Abs(delta.x)>15 || Mathf.Abs(delta.y)>5)continue;
                float score=(Mathf.Abs(delta.z)*.3f+Mathf.Abs(delta.x))*.8f;
                if(score<bestScore){bestScore=score;best=enemy.transform;offset=Vector3.up*2.5f;isCar=false;hold=glanceDuration;}
            }
            if(best==null)return;
            attention=best;attentionOffset=offset;attentionIsCar=isCar;glanceUntil=clock+hold;nextGlance=glanceUntil+glanceCooldown;GlanceCount++;
        }

        public void ReactToPass(Vector3 worldPoint,float strength,Transform passingCar=null){
            if(!CanReact || Aiming || manager.gameState!=GameState.Playing || strength<=0)return;
            combatAttention=FindCombatThreat();if(combatAttention!=null)return;
            passStrength=Mathf.Clamp01(strength);passSide=worldPoint.x>=transform.position.x ? 1 : -1;passAge=0;
            passPoint=worldPoint;passGlance=passingCar==null;attention=passingCar;attentionIsCar=passingCar!=null;attentionOffset=Vector3.up*1.5f;glanceUntil=clock+glanceDuration*.8f;nextGlance=glanceUntil+glanceCooldown;
            PassReactionCount++;GlanceCount++;
        }

        private void LateUpdate()=>ApplyPose(Time.deltaTime);
        private void ApplyPose(float dt){
            if(!initialized || dt<=0 || manager.gameState==GameState.Pause)return;
            if(!CanReact || manager.gameState!=GameState.Playing)return;
            var forward=head.forward;
            float baseYaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            float basePitch=-Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            float goalYaw=baseYaw,goalPitch=basePitch;
            bool aiming=Aiming;
            bool lookingAtCar=!aiming && combatAttention==null && (passGlance || (attention!=null && attentionIsCar)) && clock<glanceUntil;
            if(lookingAtCar)carReturning=true;
            if(aiming || combatAttention!=null || (attention!=null && !attentionIsCar)){carReturning=false;neckShare=neckVelocity=0;}
            var aimedEnemy=aiming && aim!=null ? aim.Target : null;
            var lookingEnemy=combatAttention!=null ? combatAttention : aimedEnemy;
            if((aiming && !wasAiming) || lookingEnemy!=previousAimTarget){yawVelocity=pitchVelocity=0;}
            wasAiming=aiming;previousAimTarget=lookingEnemy;poseWasAiming=aiming;
            if(combatAttention!=null){
                LookAngles(combatAttention.transform.position+Vector3.up*2.5f,aimingHeadPitchLimit,out goalYaw,out goalPitch);
            }else if(aiming && followAimTarget && aim!=null && aim.Target!=null){
                LookAngles(aim.TargetViewPoint,aimingHeadPitchLimit,out goalYaw,out goalPitch);
            }else if(!aiming && HasAttention && clock<glanceUntil){
                var point=attention!=null ? (attentionIsCar ? attention.TransformPoint(attentionOffset) : attention.position+attentionOffset) : passPoint;
                // Traffic is low beside the rider; looking at its roof must not
                // reintroduce the old gaze into the road or turn all the way back.
                if(passGlance)point.y+=1.5f;
                LookAngles(point,lookingAtCar ? 60 : ridingHeadPitchLimit,out goalYaw,out goalPitch);
                // A glance is a small change from the authored riding gaze,
                // rather than flattening the helmet's relaxed pitch.
                float pitchRange=lookingAtCar ? carPitchRange : ridingHeadPitchLimit;
                goalPitch=basePitch+Mathf.Clamp(goalPitch-basePitch,-pitchRange,pitchRange);
            }
            if(!aiming){
                goalYaw+=Mathf.Sin(clock*.85f)*idleSway;
                goalPitch+=Mathf.Sin(clock*1.7f)*idleSway*.45f;
            }
            float lookResponse=aiming || combatAttention!=null ? combatHeadResponse : lookingAtCar ? carLookResponse : carReturning ? carReturnResponse : headResponse;
            yaw=Mathf.SmoothDampAngle(yaw,goalYaw,ref yawVelocity,lookResponse,360,dt);
            pitch=Mathf.SmoothDampAngle(pitch,goalPitch,ref pitchVelocity,lookResponse,240,dt);
            if(carReturning && !lookingAtCar && Mathf.Abs(Mathf.DeltaAngle(yaw,goalYaw))<.25f && Mathf.Abs(Mathf.DeltaAngle(pitch,goalPitch))<.25f)carReturning=false;

            if(!aiming){
                float pass=passAge<passRecovery ? Mathf.Sin(Mathf.Clamp01(passAge/passRecovery)*Mathf.PI)*passStrength : 0;
                float response=1-Mathf.Exp(-dt*8);
                bodyYaw=Mathf.Lerp(bodyYaw,Mathf.Clamp(Mathf.DeltaAngle(baseYaw,yaw)*bodyTurnShare,-3,3),response);
                bodyRoll=Mathf.Lerp(bodyRoll,passSide*passBodyLean*pass+Mathf.Sin(clock*.9f)*idleSway*.35f,response);
                bodyPitch=Mathf.Lerp(bodyPitch,Mathf.Sin(clock*1.7f)*breathingAmount,response);
                chestBase=chest.localRotation;
                chest.rotation=Quaternion.Euler(bodyPitch,bodyYaw,bodyRoll)*chest.rotation;
                chestFinal=chest.localRotation;chestApplied=true;
            }else {bodyYaw=bodyRoll=bodyPitch=0;chestApplied=false;}
            headBase=head.localRotation;
            var direction=Quaternion.Euler(pitch,yaw,0)*Vector3.forward;
            neckShare=Mathf.SmoothDamp(neckShare,lookingAtCar ? carNeckShare : 0,ref neckVelocity,lookingAtCar ? carLookResponse : carReturnResponse,Mathf.Infinity,dt);
            if(neck!=null && neckShare>0 && !aiming && combatAttention==null && (lookingAtCar || carReturning)){
                neckBase=neck.localRotation;
                var turn=Quaternion.FromToRotation(head.forward,direction);
                neck.rotation=Quaternion.Slerp(Quaternion.identity,turn,neckShare)*neck.rotation;
                neckFinal=neck.localRotation;neckApplied=true;
            }
            head.rotation=Quaternion.FromToRotation(head.forward,direction)*head.rotation;
            headFinal=head.localRotation;headApplied=true;hasPose=true;
        }

        private void LookAngles(Vector3 point,float pitchLimit,out float heading,out float elevation){
            var delta=point-head.position;delta.z=Mathf.Max(1,delta.z);
            heading=Mathf.Clamp(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-headYawLimit,headYawLimit);
            elevation=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-pitchLimit,pitchLimit);
        }
        private void RenderPose(Camera view){
            if(!hasPose || !CanReact || view.cameraType!=CameraType.Game || (manager.gameState!=GameState.Playing && manager.gameState!=GameState.Pause))return;
            if(Aiming && !poseWasAiming)return;
            if(chestApplied && !Aiming)chest.localRotation=chestFinal;
            if(neckApplied)neck.localRotation=neckFinal;
            if(headApplied)head.localRotation=headFinal;
        }
        private void RemovePose(){
            if(headApplied && head!=null && Quaternion.Angle(head.localRotation,headFinal)<.01f)head.localRotation=headBase;
            if(neckApplied && neck!=null && Quaternion.Angle(neck.localRotation,neckFinal)<.01f)neck.localRotation=neckBase;
            if(chestApplied && chest!=null && Quaternion.Angle(chest.localRotation,chestFinal)<.01f)chest.localRotation=chestBase;
            hasPose=headApplied=chestApplied=neckApplied=false;
        }
        private void Clear(){attention=null;combatAttention=null;attentionIsCar=false;passGlance=carReturning=false;neckShare=neckVelocity=0;passAge=100;bodyYaw=bodyRoll=bodyPitch=0;hasPose=false;wasAiming=false;previousAimTarget=null;}
#if UNITY_EDITOR
        public void ResetReactionPreview(){
            RemovePose();Clear();clock=nextScan=nextGlance=glanceUntil=nextThreatScan=0;
            yawVelocity=pitchVelocity=0;passStrength=passSide=0;
            if(head!=null){yaw=Mathf.Atan2(head.forward.x,head.forward.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(Mathf.Clamp(head.forward.y,-1,1))*Mathf.Rad2Deg;}
        }
        public void StepReactionPreview(float seconds){Tick(seconds);ApplyPose(seconds);}
#endif
    }
}
