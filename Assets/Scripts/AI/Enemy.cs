using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI;
using CyberBiker.Combat;
using System.Linq;

using UnityEngine.Animations.Rigging;
using UnityEngine.UIElements;

public enum EnemyState {
    Spawn,
    Attack,
    Dead,
    FinisherReady,
}

public class Enemy : MonoBehaviour, IDamageable, IDamageState {
    private static readonly List<Enemy> activeEnemies = new List<Enemy>(16);
    public static IReadOnlyList<Enemy> Instances => activeEnemies;
    private void OnEnable() { if(!activeEnemies.Contains(this))activeEnemies.Add(this); }
    private void Awake() { activeEnemies.Add(this); }
    private Health vitality;
    public bool HasInitialized => vitality!=null;
    public bool IsEncounterQueued {get;private set;}
    private EnemyArrival arrival;
    private EnemyRidingController riding;
    private EnemyTrafficAvoidance trafficAvoidance;
    public bool HasLanePreference=>riding!=null;
    public float LanePreference=>riding!=null ? riding.Lane : .5f;
    public Vector3 DrivingVelocity {get;private set;}
    public float DrivingLeanDegrees=>motoMesh!=null ? Mathf.Abs(Mathf.DeltaAngle(0,motoMesh.transform.localEulerAngles.z)) : 0;
    public void SetRidingLane(float lane,Difficulty settings) {
        if(riding==null)riding=new EnemyRidingController(transform,lane,settings);
        else riding.Assign(lane,settings);
    }
    public bool IsArriving=>arrival!=null && arrival.Active;
    public bool ArrivingFromBehind=>IsArriving && arrival.FromBehind;
    public float ArrivalVisibility=>arrival!=null ? arrival.Visibility : 1;
    public void BeginArrival(GameObject rider,bool behind,float closingSpeed,float fadeTime) {
        arrival?.Dispose();arrival=new EnemyArrival(gameObject,rider,behind,closingSpeed,fadeTime);
        armed=false;ResetEncounterArms();
        CyberBiker.Audio.GameAudio.Active?.BeginEnemyArrival(this,behind);
    }
    private float encounterQueueDistance;
    public void SetEncounterRole(bool queued,float distance) {
        encounterQueueDistance=Mathf.Max(1,distance);
        if(IsEncounterQueued==queued)return;
        IsEncounterQueued=queued;state=EnemyState.Spawn;armed=false;
        StopAllCoroutines();isShootingRunning=false;ClearPath();ResetEncounterArms();
    }
    private void ResetEncounterArms() {
        CancelPendingTrajectory();
        if(animator!=null){animator.SetBool("Attack",false);animator.SetBool("GunReady",false);animator.SetLayerWeight(1,0);animator.SetLayerWeight(2,0);}
        if(animationRigging!=null){animationRigging.layers[0].rig.weight=0;animationRigging.layers[1].rig.weight=0;}
        if(gunParticle1!=null)gunParticle1.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(gunParticle2!=null)gunParticle2.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    [Header("Speed trail")]
    [SerializeField, Range(.05f,.6f)] private float speedTrailLifetime = .3f;
    [SerializeField, Range(2,25)] private float speedTrailMaxLength = 18;
    [SerializeField, Range(.05f,1.2f)] private float speedTrailWidth = .42f;
    [SerializeField, Range(0,6)] private float speedTrailBrightness = 1.75f;
    private EnemySpeedTrail speedTrail;
    public bool SpeedTrailVisible => speedTrail!=null && speedTrail.Visible;
    public void HideSpeedTrailForFinisher(){speedTrail?.Hide();}
    private Weapon weapon;
    private EnemyShotWarning shotWarning;
    private EnemyGunEffects gunEffects;
    private EnemyShotTrajectory pendingTrajectory;
    private int pendingTrajectoryGun;
    public EnemyShotTrajectory PendingTrajectory=>pendingTrajectory!=null && pendingTrajectory.IsPreviewFor(this) ? pendingTrajectory : null;
    private void CancelPendingTrajectory(bool deferred=false){if(pendingTrajectory!=null)pendingTrajectory.CancelPreview(this,deferred);pendingTrajectory=null;}
    private void PrepareTrajectory(int gunIndex,float warningTime){
        if(gameplay==null || !gameplay.ProjectileCombat.enemyTrajectoryGuides)return;
        CancelPendingTrajectory();pendingTrajectoryGun=gunIndex;
        pendingTrajectory=EnemyShotTrajectory.Create(this,GunMuzzle(gunIndex),player.transform,warningTime);
    }
    private void BeginShotWarning(int gunIndex,float warningTime){
        if(shotWarning==null){shotWarning=GetComponent<EnemyShotWarning>();if(shotWarning==null)shotWarning=gameObject.AddComponent<EnemyShotWarning>();}
        shotWarning.Begin(GunMuzzle(gunIndex),warningTime);PrepareTrajectory(gunIndex,warningTime);
    }
    private EnemyHitReaction hitReaction;
    private Gameplay.GameplayManager gameplay;
    public bool IsAlive => vitality != null && vitality.IsAlive;

    [Header("Health")]
    [Tooltip("Starting health, applied when this enemy spawns.")]
    [SerializeField, Min(1)] private int health = 100;
    [Header("Pistol stagger and sword finisher")]
    [SerializeField, Range(.1f,.8f)] private float finisherHealthFraction = .3f;
    [Tooltip("Gameplay seconds. Pause freezes this window; slow motion gives more real time.")]
    [SerializeField, Range(1,12)] private float finisherWindow = 5;
    [Tooltip("Play one warning this many gameplay seconds before expiry. Limited to the last half of short windows; zero disables it.")]
    [SerializeField, Range(0,3)] private float finisherWarningLeadTime = 1.25f;
    [Tooltip("Health restored when the opportunity expires. Always recovers above the stagger threshold.")]
    [SerializeField, Range(.2f,1)] private float finisherRecoveryHealth = .6f;
    [SerializeField, Range(.5f,1)] private float finisherSpeedMultiplier = 1;
    [Header("Finisher highlight")]
    [SerializeField] private Shader finisherHighlightShader;
    [SerializeField, InspectorName("Blue Flash Color"), ColorUsage(true,false)] private Color finisherReadyColor = new Color(.035f,.18f,.85f,1);
    [SerializeField, InspectorName("Orange Flash Color"), ColorUsage(true,false)] private Color finisherCloseColor = new Color(.8f,.42f,.04f,1);
    [Tooltip("Color changes per gameplay second at the start of the finisher window.")]
    [SerializeField, Range(.5f,6)] private float finisherFlashStartRate = 2;
    [Tooltip("Color changes per gameplay second near expiry. The switching rate accelerates toward this value.")]
    [SerializeField, Range(4,16)] private float finisherFlashEndRate = 10;
    [Tooltip("Overall highlight strength. Glow Radiance controls the edge emission that creates the halo.")]
    [SerializeField, Range(0,1)] private float finisherGlowIntensity = .9f;
    [Tooltip("Emission at the silhouette/rim that creates the soft bloom halo. Does not add emission across the whole body.")]
    [SerializeField, Range(0,1.5f)] private float finisherGlowRadiance = 1.1f;
    private float finisherRemaining;
    private bool finisherWarningPlayed;
    private EnemyFinisherGlow finisherGlow;
    public bool IsFinisherReady => IsAlive && state==EnemyState.FinisherReady;
    public float FinisherTimeRemaining => IsFinisherReady ? finisherRemaining : 0;
    public float FinisherWindow => finisherWindow;
    public float CurrentHealth => vitality!=null ? vitality.Current : health;
    public bool FinisherInRange => IsFinisherReady && playerSword!=null && playerSword.IsInFinisherRange(this);
    public int HighlightRendererCount => finisherGlow!=null ? finisherGlow.VisibleRendererCount : 0;
    public Color FinisherBlueColor => finisherReadyColor;
    public Color FinisherOrangeColor => finisherCloseColor;
    public float FinisherFlashStartRate => Mathf.Max(.5f,finisherFlashStartRate);
    public float FinisherFlashEndRate => Mathf.Max(FinisherFlashStartRate,finisherFlashEndRate);
    public Color FinisherHighlightColor => finisherGlow!=null ? finisherGlow.CurrentColor : finisherReadyColor;
    public int FiredShotCount { get; private set; }
    public float LastFiredTime {get;private set;}=float.NegativeInfinity;
    [Header("Weapon Tuning")]
    [Tooltip("Seconds between alternating left/right shots. 0.5 = 2 shots/second. Keeps the existing two-arm firing sequence.")]
    [SerializeField, Min(0.01f)] private float shootInterval = .5f;
    [Tooltip("Damage per projectile. Can be tuned during Play mode.")]
    [SerializeField, Min(0)] private float shootingDamage = 10f;

    [SerializeField] private Transform[] rayCheck;

    [SerializeField] private GameObject motoMesh;
    [SerializeField] private Transform bodyRotation;

    [SerializeField] private Transform leftBulletSpawn;
    [SerializeField] private Transform rightBulletSpawn;
    [SerializeField] private GameObject bullet;
    [SerializeField] public GameObject bulletParent;

    [SerializeField] private ParticleSystem gunParticle1;
    [SerializeField] private ParticleSystem gunParticle2;
    [SerializeField] ParticleSystem damageParticle;
    [SerializeField] ParticleSystem explosion;

    private EnemyState state;

    private GameObject player;
    private float speed;
    public float ForwardSpeed => IsArriving ? arrival.ForwardSpeed : ControlsPlayerDistance ? controlledForwardSpeed : speed*transform.forward.z;
    public float LateralSpeed => riding!=null ? DrivingVelocity.x : speed*transform.forward.x;
    private PlayerController playerController;
    private CyberBiker.Player.BikeSwordCombat playerSword;
    private Rigidbody playerBody;
    private float controlledForwardSpeed;
    // Keep the original spawn approach outside Launch so distant riders enter
    // pistol range before taking their fighting positions.
    // A missed/passed spawn catches up, but still arms only inside the original70m.
    private bool ControlsPlayerDistance => IsAlive && !IsArriving && !IsFinisherReady && playerBody!=null &&
        (IsEncounterQueued || state!=EnemyState.Spawn || (playerSword!=null && playerSword.IsPursuing) || Vector3.Dot(transform.position-playerBody.position,RoadForward)<0);
    private float PlayerReferenceSpeed => IsFinisherReady
        ? Mathf.Min(playerBody.linearVelocity.z,playerController.NormalSpeedLimit*finisherSpeedMultiplier)
        : playerBody.linearVelocity.z;
    private int moveDirection;

    private Animator animator;
    private RigBuilder animationRigging;

    private bool armed;
    public bool IsAimingWeapons => armed && IsAlive && !IsEncounterQueued && !IsArriving && !IsFinisherReady;
    public Transform GunMuzzle(int index) => index==0 ? leftBulletSpawn : rightBulletSpawn;
    private bool isShootingRunning;
    private GameObject bulletHit;

    private AIController aiConntroller;
    private int currentWaypointIndex = 1;
    private Vector3 followTarget;
    DodgePoint path;
    private DodgePoint pathRoot;
    private DodgePoint roadWaypoint;
    private WorldGenerator world;
    private RoadLaneFrame roadFrame;
    private bool hasRoad,roadCovered,recoveringNavigation;
    private float nextRoadLookup;
    private Vector3 RoadForward=>hasRoad && roadFrame.IsCurrent ? roadFrame.forward : Vector3.forward;

    // One live scene-wide setting; legacy per-enemy serialized distances are ignored.
    public float playerDistance => gameplay != null ? gameplay.EnemyDistanceFromPlayer : 20f;

    void Start() {
        vitality = new Health(Mathf.Max(1, health));
        vitality.Died += Die;
        gameplay = FindAnyObjectByType<Gameplay.GameplayManager>();
        player = GameObject.FindGameObjectWithTag("Player");
        playerController=player.GetComponent<PlayerController>();
        playerSword=player.GetComponent<CyberBiker.Player.BikeSwordCombat>();
        playerBody=player.GetComponent<Rigidbody>();
        speedTrail=new EnemySpeedTrail(gameObject);
        aiConntroller = FindAnyObjectByType<AIController>();
        world=FindAnyObjectByType<WorldGenerator>();
        trafficAvoidance=new EnemyTrafficAvoidance(gameObject,aiConntroller);

        animator = GetComponent<Animator>();
        shotWarning=GetComponent<EnemyShotWarning>() ?? gameObject.AddComponent<EnemyShotWarning>();
        gunEffects=GetComponent<EnemyGunEffects>() ?? gameObject.AddComponent<EnemyGunEffects>();
        hitReaction = GetComponent<EnemyHitReaction>() ?? gameObject.AddComponent<EnemyHitReaction>();
        animationRigging = GetComponent<RigBuilder>();
        if(IsEncounterQueued || IsArriving){speed=IsArriving ? arrival.ForwardSpeed : playerBody.linearVelocity.z;ResetEncounterArms();}
        finisherGlow=new EnemyFinisherGlow(this,finisherHighlightShader!=null ? finisherHighlightShader : Shader.Find("CyberBiker/Finisher Highlight"));
        if (bulletParent == null) {
            bulletParent = GameObject.FindGameObjectWithTag("Finish");
        }
    }

    private void LateUpdate() {
        speedTrail?.Tick(player!=null ? player.transform : null,ForwardSpeed,speedTrailLifetime,speedTrailMaxLength,speedTrailWidth,speedTrailBrightness,
            !IsAlive || ArrivalVisibility<.99f || (playerSword!=null && playerSword.HoldsEnemyForStrike(this)));
    }
    private void OnDisable(){CancelPendingTrajectory(true);shotWarning?.Cancel();speedTrail?.Hide();arrival?.Dispose();arrival=null;}
    private void Update() {
        if (!IsAlive || (gameplay != null && gameplay.gameState != Gameplay.GameState.Playing)) return;
        if(IsFinisherReady) {
            finisherRemaining=Mathf.Max(0,finisherRemaining-Time.deltaTime);
            if(finisherRemaining<=0)RecoverFromFinisher();
            else if(!finisherWarningPlayed && finisherWarningLeadTime>0 && finisherRemaining<=Mathf.Min(finisherWarningLeadTime,Mathf.Max(1,finisherWindow)*.5f)) {
                finisherWarningPlayed=true;
                CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.EnemyFinisherWarning,transform.position);
            }
        }
        finisherGlow?.Tick(Time.deltaTime,finisherGlowIntensity,finisherGlowRadiance);
        if(followTarget!=Vector3.zero && Vector3.Dot(followTarget-transform.position,RoadForward)<=0){ClearPath();recoveringNavigation=true;followTarget=RecoveryTarget();}
        if(riding!=null)riding.ApplyVisual(motoMesh.transform,Time.deltaTime);
        else {
            var targetRotation = transform.eulerAngles;
            targetRotation.z = -targetRotation.y * 2f;
            targetRotation.x = 0;
            targetRotation.y = 0;
            motoMesh.transform.localRotation = Quaternion.Slerp(
                motoMesh.transform.localRotation,Quaternion.Euler(targetRotation),Time.deltaTime * 10f);
        }
        if (followTarget != Vector3.zero) {
            if(recoveringNavigation)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(followTarget-transform.position,Vector3.up),180*Time.deltaTime);
            else transform.LookAt(followTarget, Vector3.up);
        }
        else {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(RoadForward), Time.deltaTime * 10f);
        }

        // Shooting logic when the enemy is armed
        if (armed && !IsEncounterQueued && !IsArriving && !IsFinisherReady) {
            // Gradually blend the arm layers into the animation
            animationRigging.layers[1].rig.weight = 1;
            animationRigging.layers[0].rig.weight = Mathf.Lerp(animationRigging.layers[0].rig.weight, 1, Time.deltaTime * 5f);

            if (animationRigging.layers[0].rig.weight >= 0.9f) {
                animator.SetBool("GunReady", true);
                if (!isShootingRunning)
                    Shoot(); // Trigger the shooting sequence
            }

            // Rotate the body towards the player when armed
            Vector3 targetPosition = player.transform.position;
            Debug.DrawLine(transform.position + Vector3.up * 2, targetPosition + Vector3.up * 2);
            Vector3 direction = targetPosition - transform.position;

            Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);
            bodyRotation.transform.rotation = Quaternion.RotateTowards(bodyRotation.transform.rotation, bodyTargetRotation, Time.deltaTime * 120f);

            gunParticle2.transform.LookAt(player.transform.position + Vector3.up * 3f);
            gunParticle1.transform.LookAt(player.transform.position + Vector3.up * 3f);
        }
    }

    private void FixedUpdate() {
        if (!IsAlive || (gameplay != null && gameplay.gameState != Gameplay.GameState.Playing))
            return;
        RefreshRoad();
        if(arrival!=null) {
            arrival.Tick(Time.fixedDeltaTime,IsEncounterQueued ? encounterQueueDistance : playerDistance,playerBody.linearVelocity.z);
            if(arrival.Finished){arrival.Dispose();arrival=null;}
        }
        if(path!=null && (!path.IsValid || Vector3.Dot(path.GetPosition()-transform.position,RoadForward)<=0))ClearPath();

        // Move the enemy based on the player's position and other conditions
        if (!IsEncounterQueued && !IsArriving && !IsFinisherReady && Vector3.Distance(transform.position, player.transform.position) < 70) {
            state = EnemyState.Attack;
            moveDirection = 1;
        }

        if (state == EnemyState.Attack) {
            animator.SetBool("Attack", true);
        }

        if(IsArriving) {
            speed=arrival.ForwardSpeed/Mathf.Max(.2f,transform.forward.z);
        } else if (state == EnemyState.Spawn) {
            speed = Mathf.Lerp(speed, PlayerReferenceSpeed - 30f, Time.deltaTime);
        }
        else {
            if (playerController.wallDirection != 0) {
                speed = Mathf.Lerp(speed, PlayerReferenceSpeed - 2f, Time.deltaTime);
            }
            else {
                ProcessSpeed();
            }
        }

        if(ControlsPlayerDistance) {
            var delta=transform.position-playerBody.position;
            float desiredDistance=IsEncounterQueued ? encounterQueueDistance : playerDistance;
            float sideways=delta.x*delta.x+delta.y*delta.y;
            float desiredForwardGap=Mathf.Sqrt(Mathf.Max(1,desiredDistance*desiredDistance-sideways));
            // The global Inspector distance is authoritative in normal riding
            // and Launch. Adjust velocity to approach it smoothly; never warp
            // positions or override the lateral obstacle-avoidance path.
            float error=desiredForwardGap-delta.z;
            controlledForwardSpeed=Mathf.Max(0,playerBody.linearVelocity.z+Mathf.Clamp(error*4,-30,30));
            speed=controlledForwardSpeed/Mathf.Max(.2f,transform.forward.z);
        } else if(IsFinisherReady)speed=Mathf.Min(speed,playerController.NormalSpeedLimit*finisherSpeedMultiplier);
        var movement=transform.forward*speed;
        if(IsArriving)movement.z=arrival.ForwardSpeed;
        if(ControlsPlayerDistance)movement.z=controlledForwardSpeed;
        var nextPosition=transform.position+movement*Time.fixedDeltaTime;
        if(arrival!=null)nextPosition=arrival.ConstrainOvertake(transform.position,nextPosition,Time.fixedDeltaTime);
        nextPosition=hasRoad && roadFrame.IsCurrent ? roadFrame.ConstrainStep(transform.position,nextPosition) : nextPosition;
        var safePosition=trafficAvoidance.Constrain(nextPosition,Time.fixedDeltaTime);
        // A moving car can invalidate an earlier clear route. Ask the original
        // dodge-point planner again instead of waiting behind the blocked path.
        if((safePosition-nextPosition).sqrMagnitude>.0001f)ClearPath();
        nextPosition=safePosition;
        DrivingVelocity=(nextPosition-transform.position)/Mathf.Max(.0001f,Time.fixedDeltaTime);transform.position=nextPosition;
        if(riding!=null && hasRoad && roadFrame.IsCurrent)riding.Feedback(DrivingVelocity,roadFrame);

        if (path == null) {
            followTarget = new Vector3(rayCheck[1].position.x, transform.position.y, rayCheck[1].position.z) + transform.forward * 10f;
            if(pathRoot==null)pathRoot=new DodgePoint(transform.position,gameObject);else pathRoot.Reset(transform.position,gameObject);
            if(riding!=null && hasRoad && roadFrame.IsCurrent && !IsFinisherReady && !(playerSword!=null && playerSword.HoldsEnemyForStrike(this))) {
                float ahead=Mathf.Clamp(Mathf.Abs(movement.z)*.45f,25,65);
                var point=riding.ClearRoadTarget(roadFrame,ahead);
                float angle=Vector3.SignedAngle(RoadForward,point-transform.position,Vector3.up);
                point=transform.position+Quaternion.AngleAxis(Mathf.Clamp(angle,-aiConntroller.dodgeAngleLimit*.9f,aiConntroller.dodgeAngleLimit*.9f),Vector3.up)*RoadForward*ahead;
                if(roadWaypoint==null)roadWaypoint=new DodgePoint(point,gameObject);else roadWaypoint.Reset(point,gameObject);
                path=aiConntroller.GetPath(pathRoot,roadWaypoint);
            } else path=aiConntroller.GetPath(pathRoot);
            recoveringNavigation=path==null;
            if(recoveringNavigation)followTarget=RecoveryTarget();
            currentWaypointIndex = 1;
        }

        if (path != null) {
            Vector3 currentWaypoint = new Vector3(path.GetPosition().x, transform.position.y, path.GetPosition().z);
            float step = speed * Time.deltaTime;
            followTarget = Vector3.MoveTowards(followTarget, currentWaypoint, step);

            if (followTarget.z >= currentWaypoint.z) {
                currentWaypointIndex++;
            }

            if (followTarget.z >= path.GetPosition().z) {
                ClearPath();
            }
        }
    }

    private void RefreshRoad(){
        if(world==null)return;
        bool valid=hasRoad && roadFrame.IsCurrent;
        if(valid && roadFrame.ContainsForward(transform.position))return;
        if(valid && !roadCovered && Time.time<nextRoadLookup)return;
        nextRoadLookup=Time.time+.2f;hasRoad=world.TryGetLaneFrame(transform.position,out roadFrame);roadCovered=hasRoad && roadFrame.ContainsForward(transform.position);
    }
    private Vector3 RecoveryTarget(){
        float ahead=Mathf.Max(10,Mathf.Abs(speed)*.25f);var forward=RoadForward;var direction=forward;
        if(hasRoad && roadFrame.IsCurrent){
            float lateral=Vector3.Dot(transform.position-roadFrame.origin,roadFrame.right);
            direction=forward*ahead+roadFrame.right*(Mathf.Clamp(lateral,roadFrame.min,roadFrame.max)-lateral);
            float limit=Mathf.Clamp(aiConntroller.dodgeAngleLimit,1,45);float angle=Mathf.Clamp(Vector3.SignedAngle(forward,direction,Vector3.up),-limit,limit);
            direction=Quaternion.AngleAxis(angle,Vector3.up)*forward;
        }
        var straight=transform.position+direction*ahead;
        if(riding!=null && hasRoad && roadFrame.IsCurrent && !IsFinisherReady && !(playerSword!=null && playerSword.HoldsEnemyForStrike(this))) {
            var preferred=riding.ClearRoadTarget(roadFrame,ahead);
            float angle=Vector3.SignedAngle(forward,preferred-transform.position,Vector3.up);
            preferred=transform.position+Quaternion.AngleAxis(Mathf.Clamp(angle,-aiConntroller.dodgeAngleLimit,aiConntroller.dodgeAngleLimit),Vector3.up)*forward*ahead;
            if(!Physics.CheckCapsule(transform.position+Vector3.up,preferred+Vector3.up,1,LayerMask.GetMask("WorldObject"),QueryTriggerInteraction.Ignore))return preferred;
        }
        return straight;
    }

    private void Shoot() {
        StartCoroutine(ShootArmsWithDelay());
    }

    private IEnumerator ShootArmsWithDelay() {
        isShootingRunning = true;
        // First shot with the left arm
        animator.SetLayerWeight(2, 1);
        animator.SetLayerWeight(1, 1);
        float lead=gameplay!=null ? Mathf.Max(0,gameplay.ProjectileCombat.firingWarning) : 0;
        float cadence=Mathf.Max(Mathf.Max(.01f,shootInterval),lead+(gameplay!=null ? Mathf.Max(0,gameplay.ProjectileCombat.enemyShotRecovery) : 0));
        if(lead>0){BeginShotWarning(0,lead);yield return new WaitForSeconds(lead);}
        shotWarning.Cancel();
        GunShoot(0);

        animator.SetTrigger("LeftArmShoot");
         // Play particle effect for left arm

        float rightLead=lead;
        if(cadence>rightLead)yield return new WaitForSeconds(cadence-rightLead);
        if(rightLead>0){BeginShotWarning(1,rightLead);yield return new WaitForSeconds(rightLead);}
        shotWarning.Cancel();

        // Second shot with the right arm

        GunShoot(1);
        animator.SetTrigger("RightArmShoot");
         // Play particle effect for right arm

        // Every shot gets the full warning, even when the old shot interval is
        // shorter. Keep a short white-laser recovery between red warnings.
        if(cadence>lead)yield return new WaitForSeconds(cadence-lead);

        // Reset the layers back to prevent overriding other animations
        animator.SetLayerWeight(2, 0);
        animator.SetLayerWeight(1, 0);
        isShootingRunning = false;
    }

    private void GunShoot(int gunIndex) {
        if(IsEncounterQueued || IsArriving || IsFinisherReady || !IsAlive)return;
        Transform targetGun = gunIndex == 0 ? leftBulletSpawn : rightBulletSpawn;

        Vector3 targetPosition = player.transform.position + Vector3.up * 3;

        Vector3 direction = targetPosition - targetGun.transform.position;

        Quaternion bodyTargetRotation = Quaternion.LookRotation(direction);

        if (gameplay != null && gameplay.gameState != Gameplay.GameState.Playing) return;
        if (weapon == null) weapon = new ProjectileWeapon(bullet, 0.01f, shootingDamage, bulletParent != null ? bulletParent.transform : null);
        if (weapon is ProjectileWeapon projectileWeapon) projectileWeapon.SetDamage(shootingDamage);
        var request=new ShotRequest(targetGun.position,direction,gameObject);
        if(gameplay!=null && gameplay.ProjectileCombat.enemyTrajectoryGuides && (PendingTrajectory==null || pendingTrajectoryGun!=gunIndex))PrepareTrajectory(gunIndex,0);
        if(weapon.TryFire(request, Time.time)){
            FiredShotCount++;
            LastFiredTime=Time.time;
            if(PendingTrajectory!=null){pendingTrajectory.Commit(request,targetPosition,(weapon as ProjectileWeapon)?.LastProjectile);pendingTrajectory=null;}
            shotWarning?.Fired(gunIndex);
            if(gunEffects==null)gunEffects=GetComponent<EnemyGunEffects>() ?? gameObject.AddComponent<EnemyGunEffects>();
            gunEffects.Fire(gunIndex,targetGun,direction);
        }else CancelPendingTrajectory();
    }

    private void TakeGun() {if(!IsEncounterQueued && !IsArriving)armed=true;}

    private void ClearPath() {
        path = null;
        currentWaypointIndex = 1;
        followTarget = Vector3.zero;
        recoveringNavigation=false;
    }

    private void ProcessSpeed() {
        if (moveDirection == 1 && (Vector3.Distance(transform.position, player.transform.position) < playerDistance)) {
            speed = Mathf.Lerp(speed, PlayerReferenceSpeed + 10f, Time.deltaTime * 5f);
        }
        else if (moveDirection == -1 && (Vector3.Distance(transform.position, player.transform.position) > playerDistance)) {
            speed = Mathf.Lerp(speed, PlayerReferenceSpeed - 10f, Time.deltaTime * 5f);
        }
        else {
            speed = Mathf.Lerp(speed, PlayerReferenceSpeed, Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected() {
        if (path != null) {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path.GetPosition(), 1);
            Gizmos.DrawLine(transform.position, path.GetPosition());
        }

        if (followTarget != null && followTarget != Vector3.zero) {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(followTarget, 0.5f);
        }
    }

    private void OnParticleTrigger() {
        Debug.Log(2);

        int numParticles = gunParticle1.particleCount;
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[numParticles];
        int particleCount = gunParticle1.GetParticles(particles);

        for (int i = 0; i < particleCount; i++) {
            // Logic for handling each particle when it triggers
            Debug.Log("A particle triggered the collider!");
        }
    }

    private void OnTriggerEnter(Collider other) { }

    private void OnTriggerStay(Collider other) { }

    private void OnCollisionEnter(Collision collision) {
        GetDamage(50);
    }

    public void GetDamage(int amount) { TakeDamage(new DamageInfo(amount)); }
    private bool swordDefeat;
    private float swordExplosionDelay;
    private CyberBiker.Player.SwordFinisherEffects swordEffects;
    private Vector3 swordExplosionVelocity;
    public Vector3 SwordDefeatVelocity { get; private set; }
    public bool DefeatFromSword(GameObject attacker,float forwardKick=30,float explosionDelay=.12f) {
        if(!IsFinisherReady || (gameplay!=null && gameplay.gameState!=Gameplay.GameState.Playing))return false;
        swordDefeat=true;swordExplosionDelay=Mathf.Max(.03f,explosionDelay);
        var attackerBody=attacker!=null ? attacker.GetComponent<Rigidbody>() : null;
        swordEffects=attacker!=null ? attacker.GetComponent<CyberBiker.Player.SwordFinisherEffects>() : null;
        swordExplosionVelocity=new Vector3(0,0,Mathf.Max(ForwardSpeed,attackerBody!=null ? attackerBody.linearVelocity.z : 0));
        SwordDefeatVelocity=swordExplosionVelocity+Vector3.forward*Mathf.Max(0,forwardKick);
        TakeDamage(new DamageInfo(vitality.Current,attacker));
        if(!IsAlive && attacker!=null && attacker.GetComponent<PlayerController>()!=null)CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.SwordFinisher,transform.position+Vector3.up*2);
        return !IsAlive;
    }
    public void TakeDamage(DamageInfo damage) {
        if (gameplay != null && gameplay.gameState != Gameplay.GameState.Playing) return;
        if(IsEncounterQueued || IsArriving || vitality==null || !IsAlive || damage.Amount<=0 || float.IsNaN(damage.Amount))return;
        bool pistolContact=damage.HasContact && damage.Source!=null && damage.Source.GetComponent<PlayerController>()!=null;
        bool pistolKill=pistolContact && gameplay!=null && gameplay.PistolInteractionMode==Gameplay.PistolInteraction.TapToFire && gameplay.TapPistolCanKill;
        float floor=Mathf.Max(.001f,Mathf.Min(1,vitality.Maximum*finisherHealthFraction));
        float amount=(swordDefeat || pistolKill) ? damage.Amount : Mathf.Min(damage.Amount,Mathf.Max(0,vitality.Current-floor));
        bool damaged=amount>0 && vitality.TakeDamage(new DamageInfo(amount,damage.Source));
        if(damaged && !IsAlive && !swordDefeat && pistolContact)CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.PistolKill,damage.HasContact ? damage.ContactPoint : transform.position);
        // The finisher floor prevents pistol kills, but it does not make
        // physical bullet contacts disappear. Queued/arriving immunity above
        // still suppresses reactions; health and the finisher timer stay intact.
        if(pistolContact)hitReaction?.React(damage.ContactPoint,damage.ContactNormal,damage.TravelDirection);
        else if(damaged && damageParticle!=null)Instantiate(damageParticle,transform.position+Vector3.up*2,Quaternion.identity);
        if(!swordDefeat && IsAlive && !IsFinisherReady && vitality.Current<=vitality.Maximum*(finisherHealthFraction+.000001f))EnterFinisherReady();
    }
    private void EnterFinisherReady() {
        state=EnemyState.FinisherReady;finisherRemaining=Mathf.Max(1,finisherWindow);
        finisherWarningPlayed=false;
        CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.EnemyFinisherReady,transform.position);
        StopAllCoroutines();isShootingRunning=false;
        if(gunParticle1!=null)gunParticle1.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(gunParticle2!=null)gunParticle2.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(animator!=null){animator.SetLayerWeight(1,0);animator.SetLayerWeight(2,0);animator.SetBool("GunReady",false);}
    }
    private void RecoverFromFinisher() {
        state=EnemyState.Attack;
        float recovered=vitality.Maximum*Mathf.Clamp(Mathf.Max(finisherRecoveryHealth,finisherHealthFraction+.1f),0,1);
        vitality.Heal(Mathf.Max(0,recovered-vitality.Current));
    }
    private void Die() {
        state = EnemyState.Dead;
        speedTrail?.Hide();
        finisherGlow?.Hide();
        StopAllCoroutines();
        if(swordDefeat) {
            if(animator!=null)animator.speed=0;
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            foreach(var body in GetComponentsInChildren<Rigidbody>())body.isKinematic=true;
            if(gunParticle1!=null)gunParticle1.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(gunParticle2!=null)gunParticle2.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            StartCoroutine(SwordDeath());
        } else Explode();
    }
    private IEnumerator SwordDeath() {
        float elapsed=0;
        while(elapsed<swordExplosionDelay) {
            yield return null;
            if(gameplay==null || gameplay.gameState==Gameplay.GameState.Playing) {
                float dt=Time.deltaTime;transform.position+=SwordDefeatVelocity*dt;elapsed+=dt;
            }
        }
        Explode();
    }
    private void Explode() {
        if(!swordDefeat || swordEffects==null || explosion==null)CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.Explosion,transform.position);
        if(explosion!=null){var burst=Instantiate(explosion,transform.position,Quaternion.identity);if(swordDefeat && swordEffects!=null)swordEffects.CarryExplosion(burst,swordExplosionVelocity);}
        Destroy(gameObject);
    }
    private void OnDestroy() { activeEnemies.Remove(this);arrival?.Dispose();finisherGlow?.Dispose();if (vitality != null) vitality.Died -= Die; }

    private void OnParticleCollision(GameObject other) {
        Debug.Log(1);
        GetDamage(10);
    }
}
