using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gameplay;
using UnityEngine.Animations.Rigging;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.UI;

public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("Motorcycle Movement")]
    [Tooltip("Force that pushes motorcycle the bigger the value the faster motorcycle will accelerate. Be careful if the value is too low motorcycle will not reach max speed")]
    [SerializeField] private float accelerationForce;
    [Tooltip("Normal riding speed limit in world units per second.")]
    [SerializeField] private float startMaxSpeed;
    [Tooltip("Motorcycle rotation sensitivity on Y axis")]
    [SerializeField] private float rotationSensitivity;
    [Tooltip("Force power that pushes motorcycle sideways")]
    [SerializeField] private float dodgeAccelerationForce;
    [SerializeField] private float dodgeMaxSpeed;
    [Tooltip("Maximum angle for motorcycle lean animation (it's just visual thing and does not affect the gameplay)")]
    [SerializeField] private float leanRange;
    [Header("Pistol aim response")]
    [SerializeField, Range(1,6)] private float pistolDrawSpeed = 3.5f;
    [Tooltip("Real seconds to blend into the pistol draw when Aim begins.")]
    [SerializeField, Range(.02f,.2f)] private float pistolDrawBlendTime = .045f;
    [Tooltip("Real seconds to bring the aiming arm rig in once the pistol is drawn.")]
    [SerializeField, Range(.03f,.3f)] private float aimRigBlendTime = .08f;
    [Header("Rear car collisions")]
    [Tooltip("Rear portion of the actual car collider that causes an immediate fall, including rear corners.")]
    [SerializeField, Range(.05f,.4f)] private float rearCrashZone = .18f;
    [SerializeField, Min(0)] private float minimumRearCrashSpeed = 2;

    [Header("Do not touch")]

    public Transform[] rayCheck;
    public GameObject cameraTarget;

    [HideInInspector] public float xVelocity, zVelocity;
    [HideInInspector] public float xThrow;

    [SerializeField] CameraController camera;
    [SerializeField] GameObject ragDoll;
    [SerializeField] GameObject muzzleFlash;
    [SerializeField] private GameObject bullet;

    [SerializeField] private TMP_Text healthBarValue;
    [SerializeField] private Material healthBarMaterial;
    [SerializeField] private GameObject playerMesh;
    [SerializeField] private ParticleSystem collectibleParticle;

    [SerializeField] private GameObject scope;

    private float maxSpeed;
    public Health Health { get; private set; }
    public bool IsAlive => Health != null && Health.IsAlive;
    public bool UsesAutomaticAim => autoAim!=null && autoAim.Enabled;
    public float NormalSpeedLimit => maxSpeed;
    public float ConfiguredMaxSpeed => startMaxSpeed;
    public float ForwardAcceleration => accelerationForce;
    public float SteeringAcceleration => dodgeAccelerationForce;
    public float SteeringSpeedLimit => dodgeMaxSpeed;
    public void ConfigureTestingMovement(float speedLimit, float acceleration, float steeringAcceleration, float steeringLimit) {
        startMaxSpeed = Mathf.Max(1, speedLimit);
        accelerationForce = Mathf.Max(0, acceleration);
        dodgeAccelerationForce = Mathf.Max(0, steeringAcceleration);
        dodgeMaxSpeed = Mathf.Max(0, steeringLimit);
        maxSpeed = startMaxSpeed;
        motor?.Configure(accelerationForce, dodgeAccelerationForce, dodgeMaxSpeed);
    }
    private BikeSwordCombat swordCombat;
    private bool Aiming => input.IsAiming && (swordCombat==null || !swordCombat.IsActive);
    private HealthPresenter presenter;
    private BikeMotor motor;
    private BikeImpactResponse impactResponse;
    private IPlayerInput input;
    private Weapon weapon;
    private BikePresentation presentation;
    private BikeAutoAim autoAim;

    [HideInInspector] public bool grounded;

    private bool armed;
    private bool wasAiming;
    private static readonly int PistolDrawState = Animator.StringToHash("Base Layer.Player_IdleGunGrab_001");
    private static readonly int PistolDrawSpeed = Animator.StringToHash("PistolDrawSpeed");
    public int wallDirection { get; private set; }

    private Rigidbody rb;
    private Collider[] solidColliders;
    private Animator animator;
    private TouchInputManager tim;
    private GameplayManager gm;
    private TeleportController tc;
    private RigBuilder animationRigging;
    private Transform motorcycleMesh;

    private GameObject hitObject;
    private GameObject _ragDoll;

    public bool shoot = false;

    public void PlayCollectibleParticle() {
        if (collectibleParticle != null) collectibleParticle.Play();
    }

    void Start() {
        Health = new Health(100);
        Health.Died += OnDied;
        presenter = new HealthPresenter(Health, healthBarValue, healthBarMaterial);
        rb = GetComponent<Rigidbody>();
        solidColliders = GetComponentsInChildren<Collider>(true);
        tc = GetComponent<TeleportController>();
        animator = GetComponent<Animator>();
        animationRigging = GetComponent<RigBuilder>();
        motorcycleMesh = transform.GetChild(0);
        tim = GetComponent<TouchInputManager>();
        input = tim;
        motor = new BikeMotor(rb, accelerationForce, dodgeAccelerationForce, dodgeMaxSpeed);
        impactResponse = GetComponent<BikeImpactResponse>();
        gm = FindAnyObjectByType<GameplayManager>();
        weapon = new ProjectileWeapon(bullet, gm.PlayerPistolShotInterval, gm.PlayerPistolDamage);
        weapon.Fired += OnWeaponFired;
        presentation = new BikePresentation(transform, motorcycleMesh, animator, animationRigging, rotationSensitivity, leanRange);

        maxSpeed = startMaxSpeed;

        SwitchRagDoll(false);
        autoAim=GetComponent<BikeAutoAim>();
        swordCombat=GetComponent<BikeSwordCombat>();
        TestingSettings.Initialize(this, gm, swordCombat);
        if(autoAim!=null) autoAim.Initialize(muzzleFlash!=null ? muzzleFlash.transform : null,bullet,animationRigging);
    }

    public void SwitchShoot() {
        shoot = false;
    }

    private void FixedUpdate() {
        if (gm.gameState == GameState.Playing)
            processVelocity();

        grounded = Physics.CheckSphere(transform.position - transform.up * 0.2f, 0.1f);

        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);

        wallDirection = 0;
        float leftEdge=transform.position.x, rightEdge=transform.position.x;
        // The upper body moves farther sideways when the bike banks. Keep the
        // existing wall guard ahead of that body, rather than only the low wheel box.
        foreach (var shape in solidColliders) {
            if (shape == null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy) continue;
            leftEdge=Mathf.Min(leftEdge,shape.bounds.min.x);
            rightEdge=Mathf.Max(rightEdge,shape.bounds.max.x);
        }
        for (int i = 0; i < rayCheck.Length; i++) {
            Vector3 origin=rayCheck[i].position;
            float rightDistance=Mathf.Max(1.5f,rightEdge-origin.x+.15f);
            float leftDistance=Mathf.Max(1.5f,origin.x-leftEdge+.15f);
            if (Physics.Raycast(new Ray(origin, Vector3.right), rightDistance, LayerMask.GetMask("WorldObject"))) {
                wallDirection = 1;
            } else if (Physics.Raycast(new Ray(origin, Vector3.left), leftDistance, LayerMask.GetMask("WorldObject"))) {
                wallDirection = -1;
            }
        }

        if (gm.gameState == GameState.Playing)
            xThrow = input.Steering.x == 0 ? 0 : Mathf.Sign(input.Steering.x) == wallDirection ? 0 : input.Steering.x;
        else
            xThrow = 0;

    }

    private void FireWeapon() {
        if (gm.gameState != GameState.Playing || !IsAlive || (swordCombat!=null && swordCombat.IsActive)) return;
        weapon.SetInterval(gm.PlayerPistolShotInterval);
        if(!weapon.CanFireAt(Time.time))return;
        Vector3 target = camera.transform.position + camera.transform.forward * 100f;
        if(autoAim!=null && autoAim.Enabled && input.IsAiming && autoAim.Target!=null) target=autoAim.AimPoint;
        else if (Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit)) target = hit.point;
        if (weapon is ProjectileWeapon projectileWeapon) projectileWeapon.SetDamage(gm.PlayerPistolDamage);
        weapon.TryFire(new ShotRequest(muzzleFlash.transform.position, target - muzzleFlash.transform.position, gameObject), Time.time);
    }
    private void OnWeaponFired() {
        muzzleFlash.GetComponent<MuzzleFlash>().Spawn();
        animator.SetTrigger("Shoot");
    }

    private void Update() {

        cameraTarget.transform.eulerAngles = new Vector3(0, camera.transform.eulerAngles.y + 10f, 0);

        scope.SetActive(animationRigging.layers[0].rig.weight > .2f && gm.gameState == GameState.Playing);

        bool aimNow=Aiming && gm.gameState==GameState.Playing && IsAlive;
        if(!aimNow)armed=false;
        // Speed only the draw state, in real time. Bike lean, jacket animation
        // and recoil retain their own timing, including during a finisher.
        animator.SetFloat(PistolDrawSpeed,pistolDrawSpeed/Mathf.Max(.1f,Time.timeScale));
        if(aimNow && !wasAiming)animator.CrossFadeInFixedTime(PistolDrawState,pistolDrawBlendTime*Mathf.Max(.1f,Time.timeScale),0,0);
        wasAiming=aimNow;
        UpdatePresentation(Time.deltaTime);

        if (gm.gameState == GameState.GameOver)
            maxSpeed = 0;

        autoAim?.Tick(Aiming && gm.gameState==GameState.Playing && IsAlive,armed,Time.deltaTime);

    }

    private void LateUpdate() {
        autoAim?.ApplyArmAim();
        if(input==null || gm.gameState!=GameState.Playing || !Aiming || !armed || animationRigging.layers[0].rig.weight<=.8f) return;
        if(autoAim==null || !autoAim.Enabled || autoAim.CanFire) FireWeapon();
    }

    private void TakeGun(AnimationEvent animationEvent) {
        // The authored grab clip calls TakeGun at both its start and its end.
        // Only the completion event makes the weapon ready for aiming and fire.
        if(animationEvent.time>.01f && input.IsAiming && gm.gameState==GameState.Playing) armed=true;
    }

    private void UpdatePresentation(float dt) {
        bool reacting=impactResponse!=null && gm.gameState==GameState.Playing;
        float visual=reacting ? impactResponse.PresentationSteering(xThrow,dt) : xThrow;
        presentation.AimRigBlendTime=aimRigBlendTime;
        presentation.Tick(xThrow,Aiming,armed,visual,reacting ? impactResponse.PresentationResponse : BikePresentation.DefaultPoseResponse,dt,gm.gameState==GameState.Playing ? Time.unscaledDeltaTime : 0);
    }

    private void processVelocity() {
        if(swordCombat!=null && swordCombat.StepPickupDash()) {
            motor.TickSteering(xThrow,impactResponse!=null ? impactResponse.SteeringAccelerationMultiplier(xThrow) : 1);
            xVelocity=rb.linearVelocity.x;zVelocity=rb.linearVelocity.z;return;
        }
        motor.Tick(xThrow, grounded, wallDirection, swordCombat!=null ? swordCombat.SpeedLimit(maxSpeed) : maxSpeed,
            impactResponse!=null ? impactResponse.MotorAccelerationMultiplier : 1,
            impactResponse!=null ? impactResponse.SteeringAccelerationMultiplier(xThrow) : 1,
            impactResponse!=null && impactResponse.IsRecovering);
        xVelocity = motor.LocalVelocity.x;
        zVelocity = motor.LocalVelocity.z;
    }

    public void Shoot() { FireWeapon(); }

    private void GetDamage(int amount) { TakeDamage(new DamageInfo(amount)); }
    public void TakeDamage(DamageInfo damage) {
        if (gm == null || (gm.gameState != GameState.Playing && gm.gameState != GameState.Hit)) return;
        if (Health != null && Health.TakeDamage(damage) && damage.Source != null && collectibleParticle != null)
            Instantiate(collectibleParticle, damage.Source.transform.position, Quaternion.identity, transform);
    }
    private void OnDied() { motor.Stop(); gm.SwitchGameState(GameState.GameOver); }
    private void OnDestroy() {
        presenter?.Dispose();
        if (Health != null) Health.Died -= OnDied;
        if (weapon != null) weapon.Fired -= OnWeaponFired;
    }

    public void ResetRagDoll() {
        playerMesh.SetActive(true);

        if (hitObject != null) {
            if (hitObject.transform.tag != "Wall") {
                Destroy(hitObject);
                transform.position = new Vector3(0, transform.position.y, transform.position.z);
            }
            else {
                transform.position = new Vector3(hitObject.transform.parent.transform.position.x, hitObject.transform.parent.transform.position.y, hitObject.transform.parent.transform.position.z + 10f);
            }
        }

        camera.SetTarget(cameraTarget.transform);
        impactResponse?.ResetResponse();
        tc.startFade(false);
        SwitchRagDoll(false);
    }

    private void SwitchRagDoll(bool on) {
        if (on) {
            _ragDoll = Instantiate(ragDoll, motorcycleMesh.position, motorcycleMesh.rotation);
            camera.SetTarget(_ragDoll.transform.GetChild(0).transform.GetChild(0));

            // The rider spawns overlapping the bike's upper-body colliders. Its
            // launch must not transfer momentum back into its own motorcycle.
            foreach(var riderShape in _ragDoll.GetComponentsInChildren<Collider>())
                foreach(var bikeShape in solidColliders)
                    if(bikeShape!=null) Physics.IgnoreCollision(riderShape,bikeShape,true);

            foreach (Rigidbody ragRB in _ragDoll.GetComponentsInChildren<Rigidbody>()) {
                ragRB.GetComponent<Rigidbody>().AddForce((Vector3.up / 1.5f + transform.forward) * 50f, ForceMode.VelocityChange);
            }

        } else {
            Destroy(_ragDoll);
        }
    }

    private void OnCollisionEnter(Collision collision) {
        HandleCarContact(collision,true);

        bool sideScrape = false;
        if (collision.transform.CompareTag("Wall"))
            for (int i=0;i<collision.contactCount;i++)
                if (Mathf.Abs(collision.GetContact(i).normal.x)>.65f) { sideScrape=true;break; }
        // A side scrape can reach the banked body before a low guard ray sees the
        // barrier. Use the contact normal to distinguish it from a frontal crash.
        if (collision.transform.tag == "Wall" && gm.gameState == GameState.Playing && wallDirection == 0 && !sideScrape) {
            gm.SwitchGameState(GameState.Hit);
            tc.startFade(true);
            hitObject = collision.gameObject;

            SwitchRagDoll(true);

            playerMesh.SetActive(false);
        }
    }

    private void OnCollisionStay(Collision collision) {HandleCarContact(collision,false);}

    private void HandleCarContact(Collision collision,bool initialImpact) {
        if(gm==null || gm.gameState!=GameState.Playing) return;
        if(swordCombat!=null && swordCombat.IgnoresTraffic(collision.collider))return;
        var car=collision.collider.GetComponentInParent<Car>();
        var obstacle=collision.collider.GetComponentInParent<Obstacle>();
        if(car==null && obstacle==null && !collision.transform.CompareTag("Obstacle")) return;
        Transform carRoot=car!=null ? car.transform : obstacle!=null ? obstacle.transform : collision.transform;
        if(initialImpact) impactResponse?.ReactToCollision(collision);
        var box=collision.collider as BoxCollider;
        if(box==null) box=carRoot.GetComponentInChildren<BoxCollider>();
        Vector3 carForward=box!=null ? box.transform.forward : carRoot.forward;
        if(Mathf.Abs(Vector3.Dot(collision.relativeVelocity,carForward))<minimumRearCrashSpeed) return;
        bool rearHit=false;
        for(int i=0;i<collision.contactCount;i++) {
            var contact=collision.GetContact(i);
            bool rearFace=Vector3.Dot(contact.normal,carForward)<-.35f;
            bool rearCorner=false;
            if(box!=null) {
                // Size/centre remain valid even if Car already disabled its collider.
                float localZ=box.transform.InverseTransformPoint(contact.point).z-box.center.z;
                rearCorner=localZ<=-box.size.z*.5f+box.size.z*rearCrashZone;
            }
            if(rearFace || rearCorner) {rearHit=true;break;}
        }
        if(!rearHit) return;
        gm.SwitchGameState(GameState.Hit);
        tc.startFade(true);hitObject=carRoot.gameObject;
        // Stop driving into the car immediately, retaining this collision's recoil.
        Vector3 kick=impactResponse!=null
            ? impactResponse.ApplyRearCrashKick(collision.collider,-carForward,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,carForward)))
            : Vector3.zero;
        rb.linearVelocity=new Vector3(kick.x,rb.linearVelocity.y,Mathf.Min(0,kick.z));
        xThrow=0;
        SwitchRagDoll(true);playerMesh.SetActive(false);
        if(obstacle!=null) GetDamage(obstacle.damage);
    }

    private void OnTriggerEnter(Collider other) { }

    private void OnParticleTrigger() {
        GetDamage(10);
    }

    private void OnParticleCollision(GameObject other) {
        Debug.Log("Im hit");
    }
}
