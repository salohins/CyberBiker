using System;
using System.Collections.Generic;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.Player {
    // Keep the original Humanoid Animator and its RigBuilder running. The sword
    // library is sampled on a transform-only rig, then blended onto the rider
    // after animation/IK, preserving the authored prop and jacket translations.
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class BikeSwordCombat : MonoBehaviour {
        public enum CombatPhase { Idle, Drawing, Pursuing, Striking, Sheathing, Recovering }
        public enum AttackSide { Left, Right }

        [Header("Launch")]
        [SerializeField, Range(1.05f,2)] private float speedMultiplier = 1.35f;
        [SerializeField, Min(.05f)] private float boostRiseTime = .45f;
        [SerializeField, Min(.05f)] private float boostFallTime = .5f;
        [SerializeField, Min(1)] private float launchDuration = 7;
        public float TestingSpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Clamp(value, 1, 2); }
        public float TestingLaunchDuration { get => launchDuration; set => launchDuration = Mathf.Clamp(value, 1, 30); }
        [SerializeField, Min(0)] private float cooldown = .6f;
        [SerializeField] private KeyCode launchKey = KeyCode.Space;
        [Header("Pickup targeting")]
        [SerializeField, Min(5)] private float targetRange = 150;
        [SerializeField, Min(.2f)] private float verticalReach = 3;
        [Tooltip("Enemies directly ahead use the left swing. Use the right swing only when the enemy is farther right than this distance in world units.")]
        [SerializeField, Range(0,2)] private float rightAttackSideThreshold = .5f;
        [Header("Close pickup dash")]
        [SerializeField, Range(4,25)] private float pickupRange = 14;
        [SerializeField, Range(2,14)] private float pickupSideRange = 9;
        [SerializeField, Range(.15f,.6f)] private float dashDuration = .3f;
        [SerializeField, Range(1.5f,4)] private float dashStopDistance = 2.8f;
        [Header("Finisher traffic protection")]
        [Tooltip("Real seconds of traffic protection after both the finisher animation and its slow motion finish. Cars already overlapping stay ignored until clear. Walls remain solid.")]
        [SerializeField, Range(0,3)] private float trafficRecoveryGrace = .8f;
        [Header("Sword impact")]
        [SerializeField, Range(0,60)] private float enemyForwardKick = 30;
        [SerializeField, Range(.05f,.3f)] private float enemyExplosionDelay = .12f;
        [SerializeField, Range(.5f,1.5f)] private float strikePlaybackSpeed = .95f;
        [Tooltip("Quick draw immediately before the pickup dash, in real time.")]
        [SerializeField, Range(1,6)] private float drawPlaybackSpeed = 4.5f;
        [Header("Put away after each slice")]
        [SerializeField, Range(1,6)] private float sliceReturnPlaybackSpeed = 3.5f;
        [SerializeField, Range(.03f,.3f)] private float sliceReturnBlendTime = .08f;
        [SerializeField, Range(0,.2f)] private float contactPoseHold = .08f;
        [SerializeField, Range(0,.5f)] private float chainPickupDelay = .12f;
        [Tooltip("Contact points measured from the actual blade arc, in player-local units.")]
        [SerializeField] private Vector3 leftStrikePoint = new Vector3(-1.54689f,1.34613f,-.01208f);
        [SerializeField] private Vector3 rightStrikePoint = new Vector3(1.84446f,1.24053f,.39683f);
        [Tooltip("Fraction of the attack when the blade can first hit. Damage is not applied on button press.")]
        [SerializeField, Range(.1f,.8f)] private float strikeContactTime = .5882353f;
        [SerializeField, Range(.5f,.95f)] private float strikeWindowEnd = .72f;
        [Header("Pose blending")]
        [SerializeField, Range(.03f,.3f)] private float poseBlendTime = .12f;
        [Tooltip("Real seconds to blend from the draw pose into the sword strike.")]
        [SerializeField, Range(.01f,.5f)] private float strikeBlendTime = .06f;
        [Tooltip("Blend duration on the sheathe clip's timeline. Sheathe playback speed also changes its real duration.")]
        [SerializeField, Range(.01f,1)] private float sheatheBlendTime = .12f;
        [SerializeField, Range(.08f,.6f)] private float ridingRecoveryTime = .25f;
        [Header("Fast sword to pistol transition")]
        [SerializeField, Range(1,6)] private float aimReturnPlaybackSpeed = 3.5f;
        [Tooltip("Real seconds to blend back after putting away the sword for Aim.")]
        [SerializeField, Range(.03f,.25f)] private float aimReturnBlendTime = .07f;
        [SerializeField, Range(1,15)] private float swordSteeringResponse = 5;
        [Header("Imported sword library")]
        [SerializeField] private AnimationClip draw, sheathe, idle, idleLeft, idleRight, attackLeft, attackRight;
        [SerializeField] private Transform[] poseBones;
        [Header("Launch button")]
        [SerializeField] private Button launchButton;
        [SerializeField] private TMP_Text launchLabel;

        public CombatPhase Phase { get; private set; }
        public AttackSide LastAttackSide { get; private set; }
        public bool IsActive => Phase != CombatPhase.Idle;
        public bool IsPursuing => launchRunning;
        public bool AllowsAftermathSlowMotion => IsPursuing || finishingNaturally;
        private bool finishingNaturally;
        public Enemy Target { get; private set; }
        public int HitCount { get; private set; }
        public float BoostBlend => boost;
        public bool IsAssisting => Phase==CombatPhase.Striking && !hitApplied;
        public int LaunchCount { get; private set; }
        public int SheatheCompletionCount { get; private set; }
        public int DrawCompletionCount { get; private set; }
        public float LastPickupDistance { get; private set; }
        public float SwingProgress => Phase==CombatPhase.Striking ? Mathf.Clamp01(phaseTime/AttackClip.length) : 0;
        public float SwingDuration => AttackClip!=null ? AttackClip.length : .567f;
        public float SwingRealDuration => SwingDuration/strikePlaybackSpeed;
        public bool IsDashing => Phase==CombatPhase.Striking && !hitApplied && !dashComplete;
        public bool HoldsEnemyForStrike(Enemy enemy) => Phase==CombatPhase.Striking && Target==enemy;
        public bool TrafficProtected => trafficCommitted || trafficGraceRemaining>0;
        public float TrafficGraceRemaining => trafficGraceRemaining;
        public bool IgnoresTraffic(Collider shape) => (TrafficProtected && FinisherTrafficContacts.IsTraffic(shape)) || (trafficContacts!=null && trafficContacts.Contains(shape));

        private struct Pose {
            public Vector3 position,scale; public Quaternion rotation;
            public Pose(Transform t){position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
            public static Pose Blend(Pose a,Pose b,float t) => new Pose {
                position=Vector3.Lerp(a.position,b.position,t),rotation=Quaternion.Slerp(a.rotation,b.rotation,t),scale=Vector3.Lerp(a.scale,b.scale,t)
            };
            public void Apply(Transform t){t.localPosition=position;t.localRotation=rotation;t.localScale=scale;}
        }
        private PlayerController player;
        private TouchInputManager input;
        private GameplayManager gameplay;
        private Rigidbody body;
        private GameObject sampleRoot;
        private Transform[] sampleBones;
        private Pose[] basePose, sampledPose, cachedPose, transitionPose, sparePose;
        private Pose[] mountedSwordPose;
        private bool[] swordPropBones;
        private SkinnedMeshRenderer[] skins;
        private bool[] originalMatrixUpdates;
        private readonly RaycastHit[] sightHits = new RaycastHit[48];
        private float elapsed,phaseTime,boost,nextLaunch,nextSearch,steering;
        private bool initialized,hasCachedPose,hitApplied;
        private bool returningForAim,returningAfterAttack,launchRunning;
        private bool contactPending,dashComplete;
        private Vector3 dashStart,dashVelocity,dashDirection;
        private float dashAge,nextPickup,contactHoldRemaining,dashSteeringOffset;
        private Transform sampleSword,sampleMotorcycle;
        private SwordFinisherEffects effects;
        private FinisherTrafficContacts trafficContacts;
        private bool trafficCommitted;
        private float trafficGraceRemaining;
        private struct IgnoredPair {public Collider player,enemy;public Enemy owner;}
        private readonly List<IgnoredPair> ignoredPairs=new List<IgnoredPair>();

        private void Start() {
            player=GetComponent<PlayerController>();input=GetComponent<TouchInputManager>();
            gameplay=FindAnyObjectByType<GameplayManager>();body=GetComponent<Rigidbody>();
            if(draw==null || sheathe==null || idle==null || idleLeft==null || idleRight==null || attackLeft==null || attackRight==null || poseBones==null || poseBones.Length==0) {
                Debug.LogError("Sword launch is missing its imported clips or bone bindings.",this);enabled=false;return;
            }
            var runtimeClips=Resources.Load<SwordRuntimeClips>("SwordRuntimeClips");
            if(runtimeClips==null) {
                Debug.LogError("Sword runtime curves are missing. Rebuild the runtime sword library before playing.",this);enabled=false;return;
            }
            draw=runtimeClips.Resolve(draw);sheathe=runtimeClips.Resolve(sheathe);idle=runtimeClips.Resolve(idle);
            idleLeft=runtimeClips.Resolve(idleLeft);idleRight=runtimeClips.Resolve(idleRight);
            attackLeft=runtimeClips.Resolve(attackLeft);attackRight=runtimeClips.Resolve(attackRight);
            sampleRoot=new GameObject("Sword pose sampler"){hideFlags=HideFlags.HideAndDontSave};
            sampleRoot.SetActive(false);
            var copied=new Dictionary<Transform,Transform>{{transform,sampleRoot.transform}};
            sampleBones=new Transform[poseBones.Length];
            for(int i=0;i<poseBones.Length;i++)sampleBones[i]=CopyPath(poseBones[i],copied);
            foreach(var pair in copied){if(pair.Key.name=="Sword_Main")sampleSword=pair.Value;if(pair.Key.name=="Motorcycle")sampleMotorcycle=pair.Value;}
            effects=GetComponent<SwordFinisherEffects>();
            trafficContacts=new FinisherTrafficContacts(gameObject);
            basePose=new Pose[poseBones.Length];sampledPose=new Pose[poseBones.Length];cachedPose=new Pose[poseBones.Length];
            transitionPose=new Pose[poseBones.Length];sparePose=new Pose[poseBones.Length];
            mountedSwordPose=new Pose[poseBones.Length];swordPropBones=new bool[poseBones.Length];
            for(int i=0;i<poseBones.Length;i++) {
                swordPropBones[i]=poseBones[i].name=="Sword_Main" || poseBones[i].name=="Sword_Holder";
                if(swordPropBones[i])mountedSwordPose[i]=new Pose(poseBones[i]);
            }
            skins=GetComponentsInChildren<SkinnedMeshRenderer>(true);originalMatrixUpdates=new bool[skins.Length];
            for(int i=0;i<skins.Length;i++)originalMatrixUpdates[i]=skins[i].forceMatrixRecalculationPerRender;
            initialized=true;Camera.onPreCull+=ApplyRenderPose;
        }
        private Transform CopyPath(Transform original,Dictionary<Transform,Transform> copied) {
            if(copied.TryGetValue(original,out var existing))return existing;
            var parent=CopyPath(original.parent,copied);
            var copy=new GameObject(original.name).transform;copy.SetParent(parent,false);
            new Pose(original).Apply(copy);copied.Add(original,copy);return copy;
        }
        public float SpeedLimit(float normalLimit) => normalLimit*Mathf.Lerp(1,Mathf.Max(1,speedMultiplier),boost);
        private Vector3 ContactOffset => transform.TransformVector(LastAttackSide==AttackSide.Left ? leftStrikePoint : rightStrikePoint);
        private Vector3 ContactPosition {
            get {
                // Dash directly toward the enemy and stop short on that line.
                // Animation offsets must never steer the bike into a side lane.
                var p=Target.transform.position-dashDirection*dashStopDistance;
                p.y=body.position.y;return p;
            }
        }
        private Vector3 SteeredContactPosition => ContactPosition+Vector3.right*dashSteeringOffset;
        private bool DashPathClear(Vector3 destination) {
            var movement=destination-body.position;
            if(movement.sqrMagnitude<.0025f)return true;
            int count=Physics.CapsuleCastNonAlloc(body.position+Vector3.up*.9f,body.position+Vector3.up*2.3f,.65f,movement.normalized,sightHits,movement.magnitude+.1f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!FinisherTrafficContacts.IsTraffic(sightHits[i].collider) && !sightHits[i].transform.IsChildOf(transform) && !sightHits[i].transform.IsChildOf(Target.transform))return false;
            return count<sightHits.Length;
        }
        public bool StepPickupDash() {
            if(TrafficProtected)trafficContacts?.ProtectPath(body.position,body.position+body.linearVelocity*Time.fixedDeltaTime);
            trafficContacts?.ReleaseClearPairs(TrafficProtected);
            if(IsPursuing && Target!=null && Vector3.Distance(Target.transform.position,transform.position)<pickupRange+6)IgnoreTargetContacts();
            if(Phase!=CombatPhase.Striking)return false;
            // The snap owns movement only until contact. Give steering back as
            // soon as the cut lands, even while the sword animation continues.
            if(hitApplied)return false;
            if(Target==null || !Target.IsFinisherReady)return false;
            dashAge+=Time.fixedDeltaTime/Mathf.Max(.01f,Time.timeScale);
            float t=Mathf.Clamp01(dashAge/dashDuration);
            var targetVelocity=new Vector3(Target.LateralSpeed,0,Target.ForwardSpeed);
            var end=ContactPosition+targetVelocity*(Mathf.Max(0,dashDuration-dashAge)*Time.timeScale);
            var velocity=new Vector3(body.linearVelocity.x,dashVelocity.y,dashVelocity.z);
            dashSteeringOffset+=velocity.x*Time.fixedDeltaTime;
            var position=Vector3.Lerp(dashStart,end,Mathf.SmoothStep(0,1,t))+Vector3.right*dashSteeringOffset;
            if(TrafficProtected)trafficContacts?.ProtectPath(body.position,position+velocity*Time.fixedDeltaTime);
            if(!DashPathClear(position)){ReturnSword();return false;}
            // Keep manual lateral momentum separate from the automatic approach.
            // Normal steering forces run in PlayerController during the snap too.
            body.position=position-velocity*Time.fixedDeltaTime;body.linearVelocity=velocity;
            dashComplete=t>=1;return true;
        }
        private void BeginPickup() {
            LastPickupDistance=Vector3.Distance(Target.transform.position,transform.position);
            ChangePhase(CombatPhase.Striking);contactHoldRemaining=0;
            dashStart=body.position;dashVelocity=new Vector3(0,body.linearVelocity.y,Mathf.Max(body.linearVelocity.z,SpeedLimit(player.NormalSpeedLimit)));dashAge=0;dashComplete=false;hitApplied=false;contactPending=false;
            dashSteeringOffset=0;
            IgnoreTargetContacts();
            Target.HideSpeedTrailForFinisher();
            effects?.BeginSwing();
        }
        private void BeginDraw() {
            trafficCommitted=true;trafficGraceRemaining=trafficRecoveryGrace;
            trafficContacts?.ProtectPath(body.position,ContactPosition);
            returningAfterAttack=false;ChangePhase(CombatPhase.Drawing);
            for(int i=0;i<skins.Length;i++)skins[i].forceMatrixRecalculationPerRender=true;
        }
        private void IgnoreTargetContacts() {
            if(Target==null)return;
            foreach(var a in GetComponentsInChildren<Collider>())foreach(var b in Target.GetComponentsInChildren<Collider>())
                if(a.enabled && b.enabled && !a.isTrigger && !b.isTrigger && !Physics.GetIgnoreCollision(a,b)){Physics.IgnoreCollision(a,b,true);ignoredPairs.Add(new IgnoredPair{player=a,enemy=b,owner=Target});}
        }
        private void ReleaseDashCollisions(bool force=false) {
            for(int i=ignoredPairs.Count-1;i>=0;i--) {
                var pair=ignoredPairs[i];
                if(pair.player==null || pair.enemy==null){ignoredPairs.RemoveAt(i);continue;}
                if(!force && IsPursuing && Target!=null && pair.owner==Target && Target.IsAlive && Vector3.Distance(Target.transform.position,transform.position)<pickupRange+6)continue;
                if(force || !pair.enemy.enabled || !pair.player.bounds.Intersects(pair.enemy.bounds)){Physics.IgnoreCollision(pair.player,pair.enemy,false);ignoredPairs.RemoveAt(i);}
            }
        }
        public void Launch() {
            if(!initialized || gameplay==null || gameplay.gameState!=GameState.Playing || !player.IsAlive)return;
            if(IsActive){ReturnSword();return;}
            if(Time.time<nextLaunch)return;
            input.SetAim(false);GetComponent<BikeAutoAim>()?.Tick(false,false,0);
            elapsed=0;boost=0;hitApplied=false;returningForAim=returningAfterAttack=false;launchRunning=true;finishingNaturally=false;nextSearch=0;Target=null;contactPending=false;nextPickup=0;LaunchCount++;
            EnterMountedPursuit();FindTarget();
            CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.UiClick,transform.position);
        }
        public void CancelForAim(){finishingNaturally=false;if(IsActive){returningForAim=true;ReturnSword();}gameplay?.EndCombatSlowMotion();}
        private void Update() {
            if(!initialized)return;
            if(Phase!=CombatPhase.Striking)ReleaseDashCollisions();
#if UNITY_EDITOR || UNITY_STANDALONE
            if(Input.GetKeyDown(launchKey))Launch();
#endif
            bool playing=gameplay!=null && gameplay.gameState==GameState.Playing && player.IsAlive;
            if(launchButton!=null)launchButton.interactable=playing && (IsActive || Time.time>=nextLaunch);
            if(launchLabel!=null)launchLabel.text=IsActive ? "CANCEL" : "LAUNCH";
            if(!playing) {
                if(gameplay!=null && (gameplay.gameState==GameState.Hit || gameplay.gameState==GameState.GameOver))ResetCombat();
                return;
            }
            float dt=Time.deltaTime;if(dt<=0)return;
            if(trafficCommitted) {
                bool animating=Phase==CombatPhase.Drawing || Phase==CombatPhase.Striking || Phase==CombatPhase.Sheathing || Phase==CombatPhase.Recovering;
                if(animating || gameplay.CombatSlowMotionRemaining>0)trafficGraceRemaining=trafficRecoveryGrace;
                else trafficCommitted=false;
            }
            if(!trafficCommitted)trafficGraceRemaining=Mathf.Max(0,trafficGraceRemaining-FinisherClock.UnscaledDeltaTime);
            boost=Mathf.MoveTowards(boost,launchRunning ? 1 : 0,dt/(launchRunning ? boostRiseTime : boostFallTime));
            if(!IsActive)return;
            elapsed+=dt;float playback=Phase==CombatPhase.Striking ? strikePlaybackSpeed : Phase==CombatPhase.Drawing ? drawPlaybackSpeed : Phase==CombatPhase.Sheathing ? returningForAim ? aimReturnPlaybackSpeed : returningAfterAttack ? sliceReturnPlaybackSpeed : 1 : 1;
            if(launchRunning && elapsed>=launchDuration && (Phase==CombatPhase.Sheathing || Phase==CombatPhase.Recovering)){launchRunning=false;finishingNaturally=true;}
            if(Phase==CombatPhase.Striking) {
                if(contactHoldRemaining>0)contactHoldRemaining=Mathf.Max(0,contactHoldRemaining-FinisherClock.UnscaledDeltaTime);
                else phaseTime+=FinisherClock.UnscaledDeltaTime*playback;
            } else phaseTime+=(Phase==CombatPhase.Drawing || returningForAim || returningAfterAttack ? FinisherClock.UnscaledDeltaTime : dt)*playback;
            steering=Mathf.Lerp(steering,player.xThrow,1-Mathf.Exp(-dt*swordSteeringResponse));
            UpdateTarget();
            if((Phase==CombatPhase.Drawing || (Phase==CombatPhase.Striking && !hitApplied)) && (Target==null || !Target.IsFinisherReady)) {
                returningAfterAttack=true;ReturnSword(true,launchRunning && elapsed<launchDuration);
            }
            if(Phase==CombatPhase.Drawing)phaseTime=Mathf.Min(phaseTime,draw.length);
            if(Phase==CombatPhase.Pursuing) {
                if(elapsed>=launchDuration)ReturnSword(true);
                else if(Time.time>=nextPickup && CanPrepareStrike())BeginDraw();
            } else if(Phase==CombatPhase.Striking) {
                float fraction=phaseTime/AttackClip.length;
                float previousFraction=(phaseTime-FinisherClock.UnscaledDeltaTime*playback)/AttackClip.length;
                if(!hitApplied && fraction>=strikeContactTime && previousFraction<=strikeWindowEnd) {
                    // Render the authored contact pose and resolve the hit after
                    // applying it, even if a slow frame crossed the contact time.
                    phaseTime=AttackClip.length*strikeContactTime;contactPending=dashComplete;
                }
                if(phaseTime>=AttackClip.length) {
                    returningAfterAttack=true;ReturnSword(true,elapsed<launchDuration);
                }
            } else if(Phase==CombatPhase.Sheathing)phaseTime=Mathf.Min(phaseTime,sheathe.length);
            else if(Phase==CombatPhase.Recovering && phaseTime>=RecoveryDuration && (launchRunning || returningForAim || returningAfterAttack || boost<=.0001f)) {
                if(launchRunning && elapsed<launchDuration)EnterMountedPursuit();else ResetCombat(false,true);
            }
        }
        private float RecoveryDuration => returningForAim ? aimReturnBlendTime : returningAfterAttack ? sliceReturnBlendTime : ridingRecoveryTime;
        private AnimationClip AttackClip => LastAttackSide==AttackSide.Left ? attackLeft : attackRight;
        private void ChangePhase(CombatPhase next) {
            if(hasCachedPose)Array.Copy(cachedPose,transitionPose,cachedPose.Length);
            Phase=next;phaseTime=0;
        }
        private void ReturnSword(bool naturally=false,bool keepLaunching=false){
            launchRunning=keepLaunching && elapsed<launchDuration;finishingNaturally=naturally;
            contactPending=false;contactHoldRemaining=0;Target=null;nextSearch=0;
            if(!naturally)gameplay?.EndCombatSlowMotion();
            if(Phase==CombatPhase.Pursuing){if(launchRunning)EnterMountedPursuit();else ResetCombat(false,true);return;}
            if(Phase!=CombatPhase.Sheathing && Phase!=CombatPhase.Recovering)ChangePhase(CombatPhase.Sheathing);
        }
        private void EnterMountedPursuit(){
            for(int i=0;i<poseBones.Length;i++)if(swordPropBones[i])mountedSwordPose[i].Apply(poseBones[i]);
            for(int i=0;i<skins.Length;i++)skins[i].forceMatrixRecalculationPerRender=originalMatrixUpdates[i];
            hasCachedPose=false;returningAfterAttack=returningForAim=finishingNaturally=false;hitApplied=false;Target=null;nextSearch=0;nextPickup=Time.time+chainPickupDelay;
            Phase=CombatPhase.Pursuing;phaseTime=0;
        }
        private void BeginRecovery(){if(Phase!=CombatPhase.Recovering){ChangePhase(CombatPhase.Recovering);Target=null;if(!finishingNaturally)gameplay?.EndCombatSlowMotion();}}
        private void ResetCombat(bool immediately=true,bool preserveBoost=false) {
            if(immediately){trafficCommitted=false;trafficGraceRemaining=0;trafficContacts?.Dispose();}
            if(initialized) {
                if(immediately && hasCachedPose)for(int i=0;i<poseBones.Length;i++)basePose[i].Apply(poseBones[i]);
                // Humanoid animation does not necessarily write these prop bones.
                // Never leave them holding a pose from an interrupted sword clip.
                for(int i=0;i<poseBones.Length;i++)if(swordPropBones[i])mountedSwordPose[i].Apply(poseBones[i]);
                for(int i=0;i<skins.Length;i++)if(skins[i]!=null)skins[i].forceMatrixRecalculationPerRender=originalMatrixUpdates[i];
            }
            Phase=CombatPhase.Idle;Target=null;hasCachedPose=false;launchRunning=false;if(immediately)finishingNaturally=false;returningForAim=returningAfterAttack=false;if(!preserveBoost)boost=0;nextLaunch=Time.time+cooldown;contactPending=false;dashComplete=false;
        }
        private void UpdateTarget() {
            if(!IsPursuing)return;
            if(Target!=null && !Eligible(Target))Target=null;
            if(Phase!=CombatPhase.Striking && Phase!=CombatPhase.Drawing && Time.time>=nextSearch)FindTarget();
        }
        private bool Eligible(Enemy enemy) {
            if(enemy==null || !enemy.isActiveAndEnabled || enemy.IsEncounterQueued || enemy.IsArriving || !enemy.IsFinisherReady)return false;
            var delta=enemy.transform.position-transform.position;
            return delta.z>=-3 && delta.z<=targetRange && Mathf.Abs(delta.y)<=verticalReach;
        }
        public bool IsInFinisherRange(Enemy enemy) {
            if(!Eligible(enemy))return false;
            var delta=enemy.transform.position-transform.position;
            return delta.z<=pickupRange && Mathf.Abs(delta.x)<=pickupSideRange && Vector3.ProjectOnPlane(delta,Vector3.up).magnitude<=pickupRange;
        }
        private void FindTarget() {
            nextSearch=Time.time+.15f;Enemy best=null;float score=float.PositiveInfinity;
            var enemies=Enemy.Instances;
            for(int enemyIndex=0;enemyIndex<enemies.Count;enemyIndex++) {
                var enemy=enemies[enemyIndex];
                if(!Eligible(enemy))continue;
                var delta=enemy.transform.position-transform.position;
                float cost=Mathf.Max(0,delta.z)+Mathf.Abs(delta.x)*4;
                if(delta.z>=-3 && delta.z<=pickupRange && Mathf.Abs(delta.x)<=pickupSideRange)cost-=10000;
                if(cost<score){score=cost;best=enemy;}
            }
            Target=best;
        }
        private bool CanPrepareStrike() {
            if(Target==null || !Target.IsFinisherReady)return false;
            var direction=Vector3.ProjectOnPlane(Target.transform.position-transform.position,Vector3.up).normalized;
            var relative=body.linearVelocity-new Vector3(Target.LateralSpeed,0,Target.ForwardSpeed);
            // Finish a little inside dash range rather than on its exact edge;
            // target motion and the next physics step can otherwise cancel a draw.
            float drawLead=Mathf.Clamp(Vector3.Dot(relative,direction)*(draw.length/drawPlaybackSpeed)*Time.timeScale*.8f,0,8);
            return CanStartStrike(drawLead);
        }
        private bool CanStartStrike(float drawLead=0) {
            if(Target==null || !Target.IsFinisherReady)return false;
            var delta=Target.transform.position-transform.position;
            float range=pickupRange+drawLead;
            if(delta.z< -3 || delta.z>range || Mathf.Abs(delta.x)>pickupSideRange || Vector3.ProjectOnPlane(delta,Vector3.up).magnitude>range || Mathf.Abs(delta.y)>verticalReach)return false;
            LastAttackSide=delta.x>rightAttackSideThreshold ? AttackSide.Right : AttackSide.Left;
            dashDirection=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
            if(dashDirection.sqrMagnitude<.01f)dashDirection=Vector3.forward;
            return DashPathClear(ContactPosition);
        }
        private bool CanHit() {
            if(Target==null || !Target.IsFinisherReady)return false;
            var delta=Target.transform.position-transform.position;
            return Vector3.ProjectOnPlane(delta,Vector3.up).magnitude<=dashStopDistance+1 && Mathf.Abs(delta.y)<=verticalReach && ClearStrikePath();
        }
        private bool ClearStrikePath() {
            var origin=transform.position+Vector3.up*2;
            var line=Target.transform.position+Vector3.up*2-origin;
            int count=Physics.RaycastNonAlloc(origin,line.normalized,sightHits,line.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++) {
                var t=sightHits[i].transform;
                if(IgnoresTraffic(sightHits[i].collider))continue;
                if(t.IsChildOf(transform) || t.IsChildOf(Target.transform))continue;
                return false;
            }
            return count<sightHits.Length;
        }
        private void Sample(AnimationClip clip,float time,Pose[] output) {
            clip.SampleAnimation(sampleRoot,Mathf.Clamp(time,0,clip.length));
            for(int i=0;i<sampleBones.Length;i++)output[i]=new Pose(sampleBones[i]);
        }
        private void LateUpdate() {
            if(!initialized || !IsActive || Phase==CombatPhase.Pursuing || gameplay.gameState!=GameState.Playing || Time.deltaTime<=0)return;
            for(int i=0;i<poseBones.Length;i++)basePose[i]=new Pose(poseBones[i]);
            if(!hasCachedPose)Array.Copy(basePose,transitionPose,basePose.Length);
            float poseAge=Phase==CombatPhase.Striking ? dashAge : phaseTime;
            float blendTime=Phase==CombatPhase.Striking ? strikeBlendTime : Phase==CombatPhase.Sheathing ? sheatheBlendTime : poseBlendTime;
            float blend=Mathf.SmoothStep(0,1,poseAge/Mathf.Max(.01f,blendTime));
            if(Phase==CombatPhase.Recovering) {
                float recovery=Mathf.SmoothStep(0,1,phaseTime/RecoveryDuration);
                for(int i=0;i<poseBones.Length;i++)cachedPose[i]=Pose.Blend(transitionPose[i],swordPropBones[i] ? mountedSwordPose[i] : basePose[i],recovery);
            } else {
                var clip=Phase==CombatPhase.Drawing ? draw : Phase==CombatPhase.Sheathing ? sheathe : Phase==CombatPhase.Striking ? AttackClip : idle;
                Sample(clip,Phase==CombatPhase.Pursuing ? phaseTime%clip.length : phaseTime,sampledPose);
                if(Phase==CombatPhase.Pursuing && Mathf.Abs(steering)>.001f) {
                    var side=steering<0 ? idleLeft : idleRight;Sample(side,phaseTime%side.length,sparePose);
                    for(int i=0;i<poseBones.Length;i++)sampledPose[i]=Pose.Blend(sampledPose[i],sparePose[i],Mathf.Abs(steering));
                }
                for(int i=0;i<poseBones.Length;i++)cachedPose[i]=Pose.Blend(transitionPose[i],sampledPose[i],blend);
            }
            hasCachedPose=true;ApplyCachedPose();
            if(Phase==CombatPhase.Drawing && phaseTime>=draw.length) {
                DrawCompletionCount++;
                if(CanStartStrike())BeginPickup();else {returningAfterAttack=true;ReturnSword(true,elapsed<launchDuration);}
            }
            if(Phase==CombatPhase.Sheathing && phaseTime>=sheathe.length) {
                // Apply the authored last frame before handing the pose back.
                SheatheCompletionCount++;BeginRecovery();
            }
            if(contactPending) {
                contactPending=false;
                bool clear=Target!=null && DashPathClear(SteeredContactPosition);
                if(clear){body.position=SteeredContactPosition;transform.position=body.position;body.linearVelocity=new Vector3(body.linearVelocity.x,dashVelocity.y,dashVelocity.z);}
                if(clear && CanHit()) {
                    var victim=Target;var impact=victim.transform.position+Vector3.up*ContactOffset.y;
                    var shape=victim.GetComponentInChildren<Collider>();
                    if(shape!=null && Camera.main!=null){var towardCamera=-Camera.main.transform.forward;impact=shape.ClosestPoint(impact+towardCamera*5)+towardCamera*.1f;}
                    hitApplied=victim.DefeatFromSword(gameObject,enemyForwardKick,enemyExplosionDelay);
                    if(hitApplied){HitCount++;contactHoldRemaining=contactPoseHold;effects?.PlayHit(impact,victim.SwordDefeatVelocity,LastAttackSide);}
                } else if(phaseTime>=AttackClip.length*strikeContactTime) {
                    // An intervening wall/car cancels the committed cut; never
                    // damage through an obstruction or stall at the hit frame.
                    ReturnSword();
                }
            }
        }
        public void SampleBlade(float progress,Vector3 bladeBase,Vector3 bladeTip,out Vector3 rootBase,out Vector3 rootTip) {
            if(sampleMotorcycle!=null)sampleMotorcycle.localRotation=transform.GetChild(0).localRotation;
            AttackClip.SampleAnimation(sampleRoot,Mathf.Clamp01(progress)*AttackClip.length);
            rootBase=sampleRoot.transform.InverseTransformPoint(sampleSword.TransformPoint(bladeBase));
            rootTip=sampleRoot.transform.InverseTransformPoint(sampleSword.TransformPoint(bladeTip));
        }
        private void ApplyCachedPose(){for(int i=0;i<poseBones.Length;i++)cachedPose[i].Apply(poseBones[i]);}
        private void ApplyRenderPose(Camera view){if(initialized && IsActive && hasCachedPose && view.cameraType==CameraType.Game)ApplyCachedPose();}
        private void OnDisable(){if(initialized)ResetCombat();ReleaseDashCollisions(true);}
        private void OnDestroy(){Camera.onPreCull-=ApplyRenderPose;ReleaseDashCollisions(true);trafficContacts?.Dispose();if(sampleRoot!=null)Destroy(sampleRoot);}
    }
}
