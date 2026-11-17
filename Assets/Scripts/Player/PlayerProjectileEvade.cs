using System.Collections.Generic;
using CyberBiker.Audio;
using CyberBiker.Combat;
using Gameplay;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace CyberBiker.Player {
    [DefaultExecutionOrder(12000),DisallowMultipleComponent]
    public sealed class PlayerProjectileEvade : MonoBehaviour {
        public static PlayerProjectileEvade Active {get;private set;}
        public GameplayManager Manager {get;private set;}
        public ProjectileCombatSettings Settings => Manager.ProjectileCombat;
        public Bullet Threat {get;private set;}
        public float ImpactSeconds {get;private set;} = float.PositiveInfinity;
        public bool Ready => Playing && Settings.timedEvade && !Finishing && remaining<=0 && Threat!=null && ImpactSeconds<=Settings.EvadeWindow;
        public int EvadeCount {get;private set;}
        public int PerfectCount {get;private set;}
        public float PoseAmount {get;private set;}
        public float FeedbackAmount => feedbackRemaining/.3f;
        public bool LastPerfect {get;private set;}
        private PlayerController player;
        private Rigidbody body;
        private BikeSwordCombat sword;
        private Collider[] shapes;
        private Transform torso;
        private readonly RaycastHit[] obstacles=new RaycastHit[64];
        private sealed class Attachment {public Transform root,target;public Vector3 position,appliedPosition,world,targetPosition;public Quaternion rotation,appliedRotation,worldRotation,targetRotation;public bool applied;}
        private readonly List<Attachment> attachments=new List<Attachment>(2);
        private struct RecentThreat {public Bullet shot;public int lease;public float impactAt;}
        private readonly RecentThreat[] recent=new RecentThreat[32];
        private int recentCursor;
        private float clock,remaining,duration,dodgeSide,nextReward,feedbackRemaining,assistReady;
        private bool wasReady,applied;
        private Vector3 basePosition,finalPosition;
        private Quaternion baseRotation,finalRotation;
        private bool Playing => Manager!=null && Manager.gameState==GameState.Playing && player!=null && player.IsAlive && Time.timeScale>0;
        private bool Finishing => sword!=null && (sword.Phase!=BikeSwordCombat.CombatPhase.Idle && sword.Phase!=BikeSwordCombat.CombatPhase.Pursuing || sword.TrafficProtected);
        private void Awake(){
            if(GetComponent<CloseDodgeEffects>()==null)gameObject.AddComponent<CloseDodgeEffects>();
            if(GetComponent<HelmetThreatSense>()==null)gameObject.AddComponent<HelmetThreatSense>();
            player=GetComponent<PlayerController>();body=GetComponent<Rigidbody>();sword=GetComponent<BikeSwordCombat>();
            Manager=FindAnyObjectByType<GameplayManager>();shapes=GetComponentsInChildren<Collider>(true);
            foreach(var bone in GetComponentsInChildren<Transform>(true))if(bone.name=="DEF-spine.001"){torso=bone;break;}
            foreach(var constraint in GetComponentsInChildren<MultiParentConstraint>(true)){
                var data=constraint.data;var root=data.constrainedObject;
                if(root==null || !root.name.StartsWith("Gun") || data.sourceObjects.Count!=1 || (torso!=null && root.IsChildOf(torso)))continue;
                var target=data.sourceObjects[0].transform;if(target!=null)attachments.Add(new Attachment{root=root,target=target});
            }
        }
        private void OnEnable(){Active=this;Camera.onPreRender+=BeforeRender;}
        private void OnDisable(){if(Active==this)Active=null;Camera.onPreRender-=BeforeRender;RemovePose();Clear();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatic(){Active=null;}
        private void Update(){
            RemovePose();
            if(!Playing){
                Threat=null;ImpactSeconds=float.PositiveInfinity;wasReady=false;
                if(Manager!=null && Manager.gameState!=GameState.Pause)Clear();
                return;
            }
            float dt=FinisherClock.UnscaledDeltaTime;clock+=dt;remaining=Mathf.Max(0,remaining-dt);feedbackRemaining=Mathf.Max(0,feedbackRemaining-dt);
            if(Finishing){remaining=0;Threat=null;ImpactSeconds=float.PositiveInfinity;wasReady=false;return;}
            RefreshThreat();
            if(Ready && !wasReady)SoundEvents.Emit(SoundCue.EvadeReady,transform.position);
            wasReady=Ready;
            if(Settings.incomingSlowAssist && Threat!=null && ImpactSeconds<=Settings.assistWindow && clock>=assistReady && Manager.CombatSlowMotionRemaining<=0){
                assistReady=clock+Mathf.Max(1,Settings.rewardCooldown);Manager.BeginCombatSlowMotion(Settings.assistTimeScale,.18f,Settings.slowRecovery);
            }
            if(Input.GetKeyDown(Settings.evadeKey))TryEvade();
        }
        public void RefreshThreat(){
            Threat=null;ImpactSeconds=float.PositiveInfinity;
            if(!Playing || Finishing)return;
            foreach(var shot in Bullet.ActiveShots){
                if(!TryPredict(shot,out float seconds))continue;
                if(seconds<=Settings.PerfectWindow)Remember(shot,seconds);
                if(seconds<ImpactSeconds){Threat=shot;ImpactSeconds=seconds;}
            }
        }
        // Relative movement gives time to the first enabled player shape, rather
        // than proximity to the bike centre. Timing is expressed in real seconds.
        public bool TryPredict(Bullet shot,out float realSeconds){
            realSeconds=float.PositiveInfinity;
            if(shot==null || !shot.IsHostile || shot.IgnoresPlayer(player) || !Playing)return false;
            Vector3 relative=shot.Velocity-(body!=null ? body.linearVelocity : Vector3.zero);
            float speed=relative.magnitude;if(speed<.01f)return false;
            var ray=new Ray(shot.transform.position,relative/speed);float distance=float.PositiveInfinity;
            foreach(var shape in shapes){
                if(shape==null || !shape.enabled || !shape.gameObject.activeInHierarchy)continue;
                if(shape.Raycast(ray,out var contact,speed*Mathf.Max(Settings.warningHorizon,Settings.EvadeWindow)*Time.timeScale) && contact.distance<distance)distance=contact.distance;
            }
            if(float.IsPositiveInfinity(distance))return false;
            realSeconds=distance/speed/Mathf.Max(.01f,Time.timeScale);
            if(realSeconds<0 || realSeconds>Mathf.Max(Settings.warningHorizon,Settings.EvadeWindow))return false;
            // A solid obstacle on the projectile's committed route cancels the
            // warning. Neither the source nor unrelated trigger volumes block it.
            float travel=shot.bulletSpeed*distance/speed;
            int count=Physics.RaycastNonAlloc(shot.transform.position,shot.transform.forward,obstacles,travel,~0,QueryTriggerInteraction.Collide);
            for(int i=0;i<count;i++){
                var shape=obstacles[i].collider;
                if(shape.transform.IsChildOf(transform) || shape.GetComponentInParent<Bullet>()!=null || (shot.Owner!=null && shape.transform.IsChildOf(shot.Owner.transform)))continue;
                if(shape.isTrigger && shape.GetComponentInParent<IDamageable>()==null)continue;
                return false;
            }
            return true;
        }
        private void Remember(Bullet shot,float seconds){
            for(int i=0;i<recent.Length;i++)if(recent[i].shot==shot && recent[i].lease==shot.Lease){recent[i].impactAt=clock+seconds;return;}
            recent[recentCursor]=new RecentThreat{shot=shot,lease=shot.Lease,impactAt=clock+seconds};recentCursor=(recentCursor+1)%recent.Length;
        }
        public bool TryEvade(){
            RefreshThreat();if(!Ready)return false;
            var shot=Threat;bool perfect=ImpactSeconds<=Settings.PerfectWindow;
            dodgeSide=Vector3.Dot(shot.transform.position-(transform.position+Vector3.up*2),transform.right)>0 ? -1 : 1;
            duration=Mathf.Max(.18f,Settings.recovery);remaining=duration;
            shot.Evade(player);EvadeCount++;LastPerfect=perfect;
            SoundEvents.Emit(SoundCue.BulletEvade,shot.transform.position);
            if(perfect)RewardPerfect();else feedbackRemaining=.16f;
            for(int i=0;i<recent.Length;i++)if(recent[i].shot==shot)recent[i]=default;
            Threat=null;ImpactSeconds=float.PositiveInfinity;wasReady=false;return true;
        }
        public void ObservePassedShot(Bullet shot){
            if(!Playing || Finishing || shot.IgnoresPlayer(player))return;
            for(int i=0;i<recent.Length;i++)if(recent[i].shot==shot && recent[i].lease==shot.Lease){
                var sample=recent[i];recent[i]=default;
                // A previous collision course followed by a close pass is an
                // earned steering dodge. Ordinary safe-lane passes never qualify.
                if(Mathf.Abs(clock-sample.impactAt)<=Settings.PerfectWindow+Settings.steeringDodgeGrace && !TryPredict(shot,out _))RewardPerfect();
                return;
            }
        }
        private void RewardPerfect(){
            if(clock<nextReward)return;
            nextReward=clock+Mathf.Max(0,Settings.rewardCooldown);PerfectCount++;LastPerfect=true;feedbackRemaining=.3f;
            SoundEvents.Emit(SoundCue.PerfectEvade,transform.position);
            CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.PerfectDodge,transform.position+Vector3.up*2);
            if(Settings.perfectSlowMotion && !Manager.CloseDodge.enabled && Manager.CombatSlowMotionRemaining<=0 && !Finishing)
                Manager.BeginCombatSlowMotion(Settings.perfectTimeScale,Settings.perfectSlowDuration,Settings.slowRecovery);
        }
        private void LateUpdate(){ApplyPose();}
        private void BeforeRender(Camera view){if(view==Camera.main){RemovePose();ApplyPose();}}
        private void ApplyPose(){
            PoseAmount=0;if(!Playing || Finishing || remaining<=0 || torso==null)return;
            float t=1-remaining/duration;
            float amount=t<.2f ? Mathf.SmoothStep(0,1,t/.2f) : 1-Mathf.SmoothStep(0,1,(t-.2f)/.8f);PoseAmount=amount;
            foreach(var item in attachments){item.world=item.root.position;item.worldRotation=item.root.rotation;item.targetPosition=item.target.position;item.targetRotation=item.target.rotation;}
            basePosition=torso.localPosition;baseRotation=torso.localRotation;
            torso.rotation=Quaternion.AngleAxis(Settings.duckDegrees*amount,transform.right)*Quaternion.AngleAxis(Settings.twistDegrees*amount*dodgeSide,transform.forward)*torso.rotation;
            torso.position+=(transform.right*dodgeSide*.7f-Vector3.up)*Settings.bodyShift*amount;
            finalPosition=torso.localPosition;finalRotation=torso.localRotation;applied=true;
            foreach(var item in attachments){
                var delta=item.target.rotation*Quaternion.Inverse(item.targetRotation);item.position=item.root.localPosition;item.rotation=item.root.localRotation;
                item.root.SetPositionAndRotation(item.target.position+delta*(item.world-item.targetPosition),delta*item.worldRotation);
                item.appliedPosition=item.root.localPosition;item.appliedRotation=item.root.localRotation;item.applied=true;
            }
        }
        private void RemovePose(){
            if(applied && torso!=null){if((torso.localPosition-finalPosition).sqrMagnitude<.000001f)torso.localPosition=basePosition;if(Quaternion.Angle(torso.localRotation,finalRotation)<.01f)torso.localRotation=baseRotation;}
            foreach(var item in attachments)if(item.applied && item.root!=null){if((item.root.localPosition-item.appliedPosition).sqrMagnitude<.000001f)item.root.localPosition=item.position;if(Quaternion.Angle(item.root.localRotation,item.appliedRotation)<.01f)item.root.localRotation=item.rotation;item.applied=false;}
            applied=false;PoseAmount=0;
        }
        private void Clear(){Threat=null;ImpactSeconds=float.PositiveInfinity;remaining=feedbackRemaining=0;wasReady=false;System.Array.Clear(recent,0,recent.Length);}
    }
}
