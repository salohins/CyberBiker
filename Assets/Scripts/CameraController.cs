using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using CyberBiker.Player;

[DefaultExecutionOrder(100)]
public class CameraController : MonoBehaviour {
    [SerializeField] private Vector3 targetOffset;
    [Tooltip("Base aiming offset. With dynamic shoulders enabled, X sets the shoulder distance; the road side chooses its sign.")]
    [SerializeField] private Vector3 aimOffset;
    [SerializeField] private float smoothness = 3f;
    [Header("Riding steering camera")]
    [Tooltip("Dynamic offset on x axis. Triggered by dragging the screeen left/right")]
    [SerializeField] private float xShiftOffset = -2;
    [Tooltip("Dynamic offset on Y axis. Triggered by dragging the screeen left/right. Works only one way (down)")]
    [SerializeField] private float yShiftOffset = 0.5f;
    [Tooltip("Dynamic Y axis rotation offset. Triggered by dragging the screeen left/right.")]
    [SerializeField] private float angleShiftOffset = -30f;

    [Header("Aiming steering camera")]
    [Tooltip("Sideways camera shift while steering in Aim mode. Sign controls the direction.")]
    [SerializeField] private float aimXShiftOffset = -2;
    [Tooltip("Camera height shift at full steering while aiming.")]
    [SerializeField] private float aimYShiftOffset = .5f;
    [Tooltip("Forward/back camera shift at full steering while aiming.")]
    [SerializeField] private float aimForwardShiftOffset = 2;
    [Tooltip("Camera roll in degrees at full steering while aiming. Sign controls the lean direction.")]
    [SerializeField, Range(-45,45)] private float aimAngleShiftOffset = -30;

    [Header("Dynamic aim shoulder")]
    [Tooltip("Use the inside shoulder: left camera shoulder on the road's right side, and right camera shoulder on its left side.")]
    [SerializeField] private bool dynamicAimShoulder = true;
    [Tooltip("Distance across the road centre before changing shoulders. Keeps small centre-line corrections from flipping the camera.")]
    [SerializeField, Min(.1f)] private float aimShoulderSwitchDistance = 1.5f;
    [Tooltip("Glide response between shoulders. Higher values switch faster; acceleration and braking stay smooth.")]
    [SerializeField, Range(.5f,15)] private float aimShoulderSwitchSpeed = 5;
    [Tooltip("Choose the target-facing shoulder first; use road position when there is no target.")]
    [SerializeField] private bool targetAwareAimShoulder = true;
    [SerializeField, Min(.1f)] private float aimTargetShoulderDeadZone = 1.25f;
    private float aimShoulderSide=1, aimShoulderX, aimShoulderVelocity;
    private Enemy shoulderAimTarget;
    private float targetShoulderSide=-1,lastShoulderTargetTime=-999;
    private Tile shoulderRoad;
    private Transform roadOrigin;

    [Header("Aiming pistol framing")]
    [SerializeField] private AimCameraFraming.Settings aimFraming = new AimCameraFraming.Settings();
    private AimCameraFraming aimFramer;
    [Header("Rider visibility while aiming")]
    [SerializeField] private RiderAimVisibility.Settings riderVisibility = new RiderAimVisibility.Settings();
    [SerializeField,HideInInspector] private Shader riderFadeShader;
    private RiderAimVisibility riderView;
    private BikeAutoAim playerAim;
    private Camera view;
    public Vector2 AimTargetLookAngles => aimFramer!=null ? aimFramer.LookAngles : Vector2.zero;
    public bool AimPistolVisible => aimFramer!=null && aimFramer.WeaponVisible;
    public Vector2 AimPistolCorrection => aimFramer!=null ? aimFramer.VisibilityOffset : Vector2.zero;
    public Vector3 AimFollowOffset {get;private set;}
    public float RiderViewOpacity => riderView!=null ? riderView.Opacity : 1;

    [Header("Field of view")]
    [SerializeField, Range(15,120)] private float ridingFov = 50;
    [SerializeField, Range(15,120)] private float airborneFov = 80;
    [SerializeField, Range(15,120)] private float aimFov = 40;

    [Header("Camera orientation")]
    [Tooltip("Dynamic Y axis rotation offset. Triggered by dragging the screeen left/right.")]
    [SerializeField] private float cameraXangle = 0;
    [SerializeField] private float cameraYangle = 0;

    [Header("Close pass camera shake")]
    [Tooltip("Maximum sideways/upward displacement from a close pass.")]
    [SerializeField, Range(0, 0.5f)] private float passShakePosition = 0.08f;
    [Tooltip("Maximum camera roll from a close pass, in degrees.")]
    [SerializeField, Range(0, 5f)] private float passShakeRotation = 2.25f;
    [SerializeField, Min(0.05f)] private float passShakeDuration = 0.55f;
    [Tooltip("Speed of the subtle positional movement during a pass.")]
    [SerializeField, Min(0.1f)] private float passShakeFrequency = 8f;

    private float passShakeRemaining, passShakePeak, passShakeAttack, passShakePhase;
    private float passShakeRollSign = 1, passShakeTargetRollSign = 1;
    public int PassShakeCount { get; private set; }
    public float PassShakeAmount { get; private set; }

    [Header("Collision impact recoil")]
    [SerializeField, Range(0,.6f)] private float carImpactPosition = .18f;
    [SerializeField, Range(0,8)] private float carImpactRotation = 3.5f;
    [SerializeField, Range(.1f,1)] private float carImpactDuration = .4f;
    [SerializeField, Range(1,15)] private float carImpactFrequency = 6;
    private float carImpactRemaining, carImpactElapsed, carImpactPeak;
    private float carImpactSide, carImpactFront, carImpactRecoil;
    public int CarImpactCount { get; private set; }
    public float CarImpactAmount { get; private set; }

    [Header("Speed camera vibration")]
    [Tooltip("Speed at which road vibration starts to build.")]
    [SerializeField, Min(0)] private float speedShakeStartSpeed = 25f;
    [Tooltip("Speed at which vibration reaches its maximum strength.")]
    [SerializeField, Min(1)] private float speedShakeFullSpeed = 75f;
    [SerializeField, Range(0, 0.2f)] private float speedShakePosition = 0.035f;
    [SerializeField, Range(0, 2f)] private float speedShakeRotation = 0.3f;
    [SerializeField, Min(0.1f)] private float speedShakeFrequency = 12f;
    private Rigidbody bikeBody;
    private float speedShakePhase;
    public float SpeedShakeAmount { get; private set; }

    private Transform target;
    private GameplayManager gm;
    private TouchInputManager tim;
    private Animator animator;

    private Vector3 offset;

    private float fov = 50f;
    private float velocity;
    private float rotationX, rotationY;

    private float sideOffset;
    private float rotationOffset;

    private PlayerController playerController;
    private SwordFinisherEffects swordFinisher;
    private BikeLaunchEffects launchEffects;

    private void OnEnable() {
        if(Application.isPlaying) EnsureAimHelpers();
    }

    private void EnsureAimHelpers() {
        if(playerController==null)playerController=FindAnyObjectByType<PlayerController>();
        if(playerController==null)return;
        if(view==null)view=GetComponent<Camera>();
        playerAim=playerController.GetComponent<BikeAutoAim>();
        swordFinisher=playerController.GetComponent<SwordFinisherEffects>();
        launchEffects=playerController.GetComponent<BikeLaunchEffects>();
        if(aimFramer==null)aimFramer=new AimCameraFraming(playerController);
        if(riderView==null)riderView=new RiderAimVisibility(playerController,riderFadeShader!=null ? riderFadeShader : Shader.Find("CyberBiker/Rider View Fade"));
    }

    private void Start() {
        tim = FindAnyObjectByType<TouchInputManager>();
        gm = FindAnyObjectByType<GameplayManager>();
        playerController = FindAnyObjectByType<PlayerController>();
        bikeBody = playerController.GetComponent<Rigidbody>();
        animator = GameObject.FindGameObjectWithTag("Player").GetComponent<Animator>();
        EnsureAimHelpers();
        AimFollowOffset=transform.position-playerController.transform.position;
        var world=FindAnyObjectByType<WorldGenerator>();
        roadOrigin=world!=null ? world.transform : null;
        aimShoulderSide=aimOffset.x>0 ? 1 : -1;
        aimShoulderX=aimShoulderVelocity=0;
    }

    private void Update() {
        sideOffset = Mathf.Lerp(sideOffset, playerController.xThrow, Time.deltaTime);
        UpdateAimShoulder(Time.deltaTime);
        ProcessRotation();

    }

    private void LateUpdate() {
        var gunFeedback=ShootingFeedback.Active;
        gunFeedback?.RemoveCamera(view);

        Vector3 targetOffset;

        velocity = Mathf.Lerp(velocity, 5f, Time.deltaTime);

        if (!(gm.gameState == GameState.Hit)) {
            if (tim.aim) {
                targetOffset = aimOffset;
                // Shoulder placement is a separate camera channel. Keeping it
                // out of this follow offset preserves steering X-shift response.
                targetOffset.x=0;
                targetOffset -= transform.right * sideOffset * aimXShiftOffset;
                targetOffset += transform.up * Mathf.Abs(sideOffset) * aimYShiftOffset;
                targetOffset += transform.forward * Mathf.Abs(sideOffset) * aimForwardShiftOffset;

                fov = Mathf.Lerp(fov, aimFov, Time.deltaTime * 1.5f);
                GetComponent<Camera>().fieldOfView = fov;
                //frameX = Mathf.Lerp(frameX, rotationX, Time.deltaTime * 5f);
                //frameY = Mathf.Lerp(frameY, rotationY, Time.deltaTime * 5f);
                //aimTarget.transform.rotation = transform.rotation;

                //aimTarget.transform.rotation = transform.rotation;

                //animator.SetFloat("dragX", rotationY);

            }
            else {
                targetOffset = this.targetOffset;
                targetOffset -= transform.right * sideOffset * xShiftOffset;
                targetOffset += transform.up * Mathf.Abs(sideOffset) * yShiftOffset;
                targetOffset += transform.forward * Mathf.Abs(sideOffset) * 2;

                if (playerController.grounded) {
                    fov = Mathf.Lerp(fov, ridingFov, Time.deltaTime * 1.5f);
                    GetComponent<Camera>().fieldOfView = fov;
                }
                else {
                    fov = Mathf.Lerp(fov, airborneFov, Time.deltaTime * 1f);
                    GetComponent<Camera>().fieldOfView = fov;
                }

            }

        }
        else {
            targetOffset = new Vector3(0, 0, this.targetOffset.z - 2f);
            fov = Mathf.Lerp(fov, 60f, Time.deltaTime * 3f);
            GetComponent<Camera>().fieldOfView = fov;
        }

        offset = Vector3.Lerp(
            offset,
            targetOffset,
            Time.deltaTime * smoothness
        );
        transform.position = target.position - transform.TransformDirection(offset);
        transform.position-=transform.right*aimShoulderX;
        AimFollowOffset=transform.position-playerController.transform.position;
        aimFramer?.Apply(view,aimFraming,tim.aim && gm.gameState==GameState.Playing,Time.deltaTime,RiderViewOpacity);
        ApplySpeedShake();
        ApplyPassShake();
        UpdateCarImpact(Time.deltaTime);
        ApplyCarImpactPose();
        launchEffects?.ApplyCamera(view);
        swordFinisher?.ApplyCamera(view);
        riderView?.Update(view,riderVisibility,tim.aim && gm.gameState==GameState.Playing,Time.deltaTime);
        gunFeedback?.ApplyCamera(view);

        //aimTarget.transform.position = transform.position + transform.forward * 30f;
        //aimTarget.transform.rotation = transform.rotation;

    }

    private void ProcessRotation() {

        if (!(gm.gameState == GameState.Hit)) {
            if (tim.aim) {
                rotationY += tim.delta.x * Time.deltaTime * 6f;
                rotationX -= tim.delta.y * Time.deltaTime * 6f;

                rotationY = 0;
                rotationX = 0;
                if(!playerController.UsesAutomaticAim) {
                    animator.SetFloat("dragY", rotationX);
                    animator.SetFloat("CamAngle", rotationY);
                }

            }
            else {
                rotationX = Mathf.Lerp(rotationX, 0, Time.deltaTime * 5f);
                rotationY = Mathf.Lerp(rotationY, 0, Time.deltaTime * 5f);
            }
        }
        else {
            rotationX = Mathf.Lerp(rotationX, 40f, Time.deltaTime);
            rotationY = 0;
        }
        Vector3 targetRotation;
        Vector3 targetRotationShift = new Vector3(rotationX, rotationY, 0);

        rotationOffset = Mathf.Lerp(rotationOffset, playerController.xThrow * (tim.aim ? aimAngleShiftOffset : angleShiftOffset), Time.deltaTime);

        if (tim.aim) {
            cameraXangle = Mathf.Lerp(cameraXangle, 0, Time.deltaTime * 2f);
        } else {
            cameraXangle = Mathf.Lerp(cameraXangle, 2, Time.deltaTime * 1f);
        }

        targetRotation = new Vector3(cameraXangle, cameraYangle, rotationOffset);

        transform.rotation = Quaternion.Euler(targetRotation + targetRotationShift);
    }

    private void UpdateAimShoulder(float dt) {
        if(dt<=0) return;
        bool playing=gm.gameState==GameState.Playing;
        if(dynamicAimShoulder && playing) {
            Vector3 position=playerController.transform.position;
            if(Physics.Raycast(position+Vector3.up*5,Vector3.down,out var hit,100,LayerMask.GetMask("Water"),QueryTriggerInteraction.Collide)) {
                var tile=hit.collider.GetComponentInParent<Tile>();
                if(tile!=null) shoulderRoad=tile;
            }
            Vector3 centre=shoulderRoad!=null ? shoulderRoad.transform.position : roadOrigin!=null ? roadOrigin.position : Vector3.zero;
            Vector3 right=shoulderRoad!=null ? shoulderRoad.transform.right : roadOrigin!=null ? roadOrigin.right : Vector3.right;
            right=Vector3.ProjectOnPlane(right,Vector3.up).normalized;
            float lateral=Vector3.Dot(position-centre,right);
            float threshold=Mathf.Max(.1f,aimShoulderSwitchDistance);
            if(lateral>threshold) aimShoulderSide=1;
            else if(lateral<-threshold) aimShoulderSide=-1;
            var enemy=targetAwareAimShoulder && tim.aim && playerAim!=null ? playerAim.Target : null;
            if(enemy!=null) {
                float targetLateral=Vector3.Dot(playerAim.TargetViewPoint-position,right);
                if(enemy!=shoulderAimTarget) targetShoulderSide=-1;
                if(targetLateral>aimTargetShoulderDeadZone)targetShoulderSide=-1;
                else if(targetLateral<-aimTargetShoulderDeadZone)targetShoulderSide=1;
                shoulderAimTarget=enemy;lastShoulderTargetTime=Time.time;
                aimShoulderSide=targetShoulderSide;
            } else if(targetAwareAimShoulder && tim.aim && Time.time-lastShoulderTargetTime<.35f) {
                aimShoulderSide=targetShoulderSide;
            }
        }
        // Keep the side choice through the centre. Blend this channel once,
        // independently of steering, and fade it away when leaving Aim.
        float desired=tim.aim && playing ? (dynamicAimShoulder ? Mathf.Abs(aimOffset.x)*aimShoulderSide : aimOffset.x) : 0;
        // Retain shoulder velocity across frames so a side change eases into
        // motion and brakes gently, instead of starting at maximum speed.
        float glideTime=2f/Mathf.Max(.5f,aimShoulderSwitchSpeed);
        aimShoulderX=Mathf.SmoothDamp(aimShoulderX,desired,ref aimShoulderVelocity,glideTime,Mathf.Infinity,dt);
    }

    private void UpdateSpeedShake(float speed, float dt) {
        float fullSpeed = Mathf.Max(speedShakeStartSpeed + 1, speedShakeFullSpeed);
        float strength = Mathf.InverseLerp(speedShakeStartSpeed, fullSpeed, speed);
        strength = Mathf.SmoothStep(0, 1, strength);
        SpeedShakeAmount = Mathf.Lerp(SpeedShakeAmount, strength, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 5));
        speedShakePhase += Mathf.Max(0, dt) * Mathf.Max(0.1f, speedShakeFrequency);
    }

    private void ApplySpeedShake() {
        float speed = bikeBody != null && gm.gameState == GameState.Playing
            ? Vector3.ProjectOnPlane(bikeBody.linearVelocity, Vector3.up).magnitude : 0;
        UpdateSpeedShake(speed, Time.deltaTime);
        if (gm.gameState != GameState.Playing && gm.gameState != GameState.Pause) return;
        float amount = SpeedShakeAmount * (playerController.grounded ? 1f : 0.35f);
        float x = Mathf.PerlinNoise(speedShakePhase, 7.13f) * 2 - 1;
        float y = Mathf.PerlinNoise(9.71f, speedShakePhase) * 2 - 1;
        float roll = Mathf.PerlinNoise(speedShakePhase, 12.83f) * 2 - 1;
        transform.position += (transform.right * x + transform.up * y) * (speedShakePosition * amount);
        transform.rotation *= Quaternion.Euler(y * speedShakeRotation * amount, 0, roll * speedShakeRotation * amount);
    }

    // Near-pass events can trigger this without affecting movement or aim.
    public void TriggerPassShake(float strength, float rollDirection = 1f) {
        strength = Mathf.Clamp01(strength);
        if (strength <= 0) return;
        float duration = Mathf.Max(0.05f, passShakeDuration);
        float current = passShakePeak * Mathf.Clamp01(passShakeRemaining / duration);
        passShakeTargetRollSign = rollDirection < 0 ? -1f : 1f;
        if (passShakeRemaining <= 0) {
            passShakeAttack = 0;
            passShakeRollSign = passShakeTargetRollSign;
        }
        passShakePeak = Mathf.Max(current, strength);
        passShakeRemaining = duration;
        PassShakeCount++;
    }

    private void ApplyPassShake() {
        if (passShakeRemaining <= 0) { PassShakeAmount = 0; return; }
        float dt = Time.deltaTime;
        float duration = Mathf.Max(0.05f, passShakeDuration);
        passShakeAttack = Mathf.MoveTowards(passShakeAttack, 1, dt / 0.09f);
        passShakeRollSign = Mathf.MoveTowards(passShakeRollSign, passShakeTargetRollSign, dt * (2f / 0.12f));
        passShakePhase += dt * Mathf.Max(0.1f, passShakeFrequency);
        float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(passShakeRemaining / duration));
        float attack = Mathf.SmoothStep(0, 1, passShakeAttack);
        PassShakeAmount = passShakePeak * fade * attack;
        float x = Mathf.PerlinNoise(passShakePhase, 1.37f) * 2 - 1;
        float y = Mathf.PerlinNoise(2.14f, passShakePhase) * 2 - 1;
        transform.position += (transform.right * x + transform.up * y) * (passShakePosition * PassShakeAmount);
        transform.rotation *= Quaternion.Euler(0, 0, passShakeRollSign * passShakeRotation * PassShakeAmount);
        passShakeRemaining = Mathf.Max(0, passShakeRemaining - dt);
        if (passShakeRemaining == 0) { passShakePeak = 0; passShakeAttack = 0; }
    }

    public void SetTarget(Transform target) {
        this.target = target;
        offset = targetOffset;
        aimShoulderX=aimShoulderVelocity=0;
        aimFramer?.Reset();
        riderView?.Reset();lastShoulderTargetTime=-999;shoulderAimTarget=null;
        transform.position = target.position - transform.TransformDirection(offset);
    }

    public void TriggerCarImpact(float strength,Vector3 contactNormal) {TriggerCollisionImpact(strength,contactNormal);}
    public void TriggerCollisionImpact(float strength,Vector3 contactNormal) {
        strength=Mathf.Clamp01(strength);
        if(strength<=0) return;
        float duration=Mathf.Max(.1f,carImpactDuration);
        carImpactPeak=Mathf.Max(strength,carImpactPeak*Mathf.Clamp01(carImpactRemaining/duration));
        carImpactSide=Mathf.Clamp(Vector3.Dot(contactNormal,transform.right),-1,1);
        carImpactFront=Mathf.Clamp(-Vector3.Dot(contactNormal,transform.forward),-1,1);
        carImpactElapsed=0;carImpactRemaining=duration;
        CarImpactCount++;
    }

    private void UpdateCarImpact(float dt) {
        if(carImpactRemaining<=0) {CarImpactAmount=carImpactRecoil=0;return;}
        dt=Mathf.Max(0,dt);
        carImpactElapsed+=dt;
        carImpactRemaining=Mathf.Max(0,carImpactRemaining-dt);
        float attack=Mathf.SmoothStep(0,1,Mathf.Clamp01(carImpactElapsed/.035f));
        float fade=Mathf.Pow(Mathf.Clamp01(carImpactRemaining/Mathf.Max(.1f,carImpactDuration)),2);
        CarImpactAmount=carImpactPeak*attack*fade;
        carImpactRecoil=Mathf.Sin(carImpactElapsed*Mathf.Max(1,carImpactFrequency)*Mathf.PI*2)*CarImpactAmount;
        if(carImpactRemaining==0) {carImpactPeak=0;CarImpactAmount=carImpactRecoil=0;}
    }

    private void ApplyCarImpactPose() {
        if(CarImpactAmount<=0) return;
        float roll=carImpactSide*carImpactRotation*carImpactRecoil;
        Vector3 kick=new Vector3(carImpactSide*carImpactRecoil,
            .25f*carImpactRecoil,-carImpactFront*.7f*carImpactRecoil)*carImpactPosition;
        transform.position+=transform.TransformDirection(kick);
        transform.rotation*=Quaternion.Euler(carImpactFront*carImpactRotation*carImpactRecoil,
            carImpactSide*carImpactRotation*.25f*carImpactRecoil,roll);
    }
    private void OnDisable() {riderView?.Dispose();riderView=null;aimFramer=null;}
    private void OnDestroy() {riderView?.Dispose();}
}
