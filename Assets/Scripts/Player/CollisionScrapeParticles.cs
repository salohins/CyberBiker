using Gameplay;
using UnityEngine;

namespace CyberBiker.Player {
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CollisionScrapeParticles : MonoBehaviour {
        [SerializeField] private Material sparkMaterial;
        [Header("Impact and sliding")]
        [SerializeField, Min(0)] private int impactBurstCount = 8;
        [SerializeField, Min(0)] private float scrapeEmission = 360;
        [SerializeField, Min(0)] private float minimumImpactSpeed = 2;
        [SerializeField, Min(0)] private float minimumScrapeSpeed = .5f;
        [SerializeField, Min(1)] private float fullIntensitySpeed = 65;
        [Tooltip("Small separation allowed after a real hit so contact jitter does not break the spray.")]
        [SerializeField, Range(0,.3f)] private float contactTolerance = .1f;
        [Header("Spark appearance")]
        [SerializeField, Range(.01f,.2f)] private float sparkSize = .035f;
        [SerializeField, Range(1,18)] private float streakLength = 12;
        [SerializeField, Range(.1f,.6f)] private float sparkLifetime = .3f;
        [SerializeField, Range(0,4)] private float brightness = 1.8f;
        [SerializeField, Min(0)] private float outwardSpray = .8f;
        [SerializeField] private Color sparkColor = new Color(1,.58f,.16f,1);
        [Header("Collision camera feedback")]
        [InspectorName("Impact Camera Feedback")]
        [SerializeField] private bool carImpactFeedback = true;
        [SerializeField, Min(1)] private float fullCameraImpactSpeed = 18;
        [SerializeField, Range(.05f,1)] private float cameraImpactCooldown = .25f;
        private CameraController impactCamera;
        private float nextCameraImpact, lastCameraImpactStrength;

        private readonly ContactPoint[] contacts = new ContactPoint[16];
        private sealed class ScrapeContact {
            public Collider Surface;
            public Collider Vehicle;
            public Vector3 LocalNormal;
            public float Budget;
        }
        private readonly ScrapeContact[] activeContacts = new ScrapeContact[8];
        private readonly System.Random random = new System.Random(82517);
        private Rigidbody body;
        private BikeSwordCombat finisher;
        private BoxCollider bikeCollider;
        private GameplayManager manager;
        private ParticleSystem particles;
        private Material material;
        private float appliedBrightness = -1;
        public int ImpactCount { get; private set; }
        public int EmittedCount { get; private set; }
        public int ScrapeEmittedCount { get; private set; }
        public Vector3 LastContactPoint { get; private set; }
        public Vector3 LastContactNormal { get; private set; }
        public int ParticleCount => particles != null ? particles.particleCount : 0;

        private void Awake() {
            finisher=GetComponent<BikeSwordCombat>();
            body = GetComponent<Rigidbody>();
            bikeCollider = GetComponent<BoxCollider>();
            manager = FindAnyObjectByType<GameplayManager>();
            for (int i=0;i<activeContacts.Length;i++) activeContacts[i]=new ScrapeContact();
        }

        private bool CanEmit() {
            if (!isActiveAndEnabled || sparkMaterial == null || Time.timeScale <= 0) return false;
            if (manager == null) return true;
            return manager.gameState == GameState.Playing || manager.gameState == GameState.Hit;
        }

        private bool IsSurface(Collider other, Vector3 normal) {
            if(finisher!=null && finisher.IgnoresTraffic(other))return false;
            return ImpactSurfaces.IsImpactSurface(other,normal);
        }

        private bool ReadContact(Collision collision, out Vector3 point, out Vector3 normal, out Vector3 relativeMotion, out Collider surface, out Collider vehicle) {
            point = normal = relativeMotion = Vector3.zero;
            surface = null;
            vehicle = null;
            int count = collision.GetContacts(contacts), valid = 0;
            bool bodyContact=false;
            for (int i = 0; i < count; i++) {
                if (!IsSurface(contacts[i].otherCollider, contacts[i].normal)) continue;
                bool onBody=contacts[i].thisCollider!=bikeCollider;
                if (bodyContact && !onBody) continue;
                if (onBody && !bodyContact) { point=normal=Vector3.zero;valid=0;bodyContact=true; }
                point += contacts[i].point;
                normal += contacts[i].normal;
                surface = contacts[i].otherCollider;
                vehicle = contacts[i].thisCollider;
                valid++;
            }
            if (valid == 0) return false;
            point /= valid;
            normal = normal.normalized;
            if (Vector3.Dot(normal, body.worldCenterOfMass - point) < 0) normal = -normal;
            relativeMotion = body.GetPointVelocity(point);
            if (collision.rigidbody != null) relativeMotion -= collision.rigidbody.GetPointVelocity(point);
            LastContactPoint = point;
            LastContactNormal = normal;
            return true;
        }

        private void OnCollisionEnter(Collision collision) {
            if (!CanEmit() || !ReadContact(collision, out var point, out var normal, out var motion, out var surface, out var vehicle)) return;
            float speed = Mathf.Max(collision.relativeVelocity.magnitude, motion.magnitude);
            if (speed < minimumImpactSpeed) return;
            TrackContact(surface,normal,vehicle);
            TriggerImpactFeedback(surface,normal,collision.relativeVelocity);
            if (impactBurstCount == 0) return;
            float strength = Mathf.Clamp01(speed / Mathf.Max(1, fullIntensitySpeed));
            int count = Mathf.Clamp(Mathf.RoundToInt(impactBurstCount * Mathf.Lerp(.45f,1,strength)),1,64);
            Emit(point, normal, motion, count, strength);
            ImpactCount++;
        }

        private void OnCollisionStay(Collision collision) {
            if (!CanEmit() || !ReadContact(collision, out _, out var normal, out var motion, out var surface, out var vehicle)) return;
            if (motion.magnitude >= minimumScrapeSpeed) TrackContact(surface,normal,vehicle);
        }

        private void TriggerImpactFeedback(Collider surface,Vector3 normal,Vector3 relativeVelocity) {
            if(!carImpactFeedback) return;
            if(!ImpactSurfaces.IsImpactSurface(surface,normal)) return;
            float speed=Mathf.Abs(Vector3.Dot(relativeVelocity,normal));
            if(speed<.5f) return;
            float strength=Mathf.Lerp(.2f,1,Mathf.InverseLerp(.5f,Mathf.Max(1,fullCameraImpactSpeed),speed));
            if(Time.time<nextCameraImpact && strength<=lastCameraImpactStrength) return;
            if(impactCamera==null && Camera.main!=null) impactCamera=Camera.main.GetComponent<CameraController>();
            if(impactCamera==null) return;
            impactCamera.TriggerCollisionImpact(strength,normal);
            nextCameraImpact=Time.time+cameraImpactCooldown;lastCameraImpactStrength=strength;
        }

        private void TrackContact(Collider surface,Vector3 normal,Collider vehicle) {
            ScrapeContact available = null;
            foreach (var contact in activeContacts) {
                if (contact.Surface == surface) {
                    contact.LocalNormal=surface.transform.InverseTransformDirection(normal);
                    contact.Vehicle=vehicle;
                    return;
                }
                if (contact.Surface == null && available == null) available=contact;
            }
            if (available == null) return;
            available.Surface=surface;available.Budget=0;
            available.Vehicle=vehicle;
            available.LocalNormal=surface.transform.InverseTransformDirection(normal);
        }

        private bool RefreshContact(ScrapeContact contact,out Vector3 point,out Vector3 normal,out Vector3 motion) {
            point=normal=motion=Vector3.zero;
            var surface=contact.Surface;
            var vehicle=contact.Vehicle;
            if (surface == null || !surface.gameObject.activeInHierarchy || vehicle == null || !vehicle.enabled || !vehicle.gameObject.activeInHierarchy) return false;
            normal=surface.transform.TransformDirection(contact.LocalNormal).normalized;
            if(!IsSurface(surface,normal))return false;
            Vector3 center=vehicle.bounds.center;
            if (surface is BoxCollider box) {
                // Cars disable their physics collider after a hit. Its box geometry
                // still gives a valid visual contact until the bike has separated.
                Vector3 local=box.transform.InverseTransformPoint(center)-box.center;
                Vector3 extent=box.size*.5f;
                local=new Vector3(Mathf.Clamp(local.x,-extent.x,extent.x),Mathf.Clamp(local.y,-extent.y,extent.y),Mathf.Clamp(local.z,-extent.z,extent.z));
                Vector3 face=contact.LocalNormal;
                int axis=Mathf.Abs(face.x)>Mathf.Abs(face.y) ? (Mathf.Abs(face.x)>Mathf.Abs(face.z) ? 0 : 2) : (Mathf.Abs(face.y)>Mathf.Abs(face.z) ? 1 : 2);
                local[axis]=extent[axis]*Mathf.Sign(face[axis]);
                point=box.transform.TransformPoint(local+box.center);
            } else {
                if (!surface.enabled || !surface.Raycast(new Ray(center,-normal),out var hit,vehicle.bounds.extents.magnitude+contactTolerance)) return false;
                point=hit.point;normal=hit.normal;
            }
            if (Vector3.Distance(vehicle.ClosestPoint(point),point)>contactTolerance) return false;
            motion=body.GetPointVelocity(point);
            if (surface.attachedRigidbody != null) motion-=surface.attachedRigidbody.GetPointVelocity(point);
            return true;
        }

        private void FixedUpdate() {
            if (!CanEmit()) return;
            foreach (var contact in activeContacts) {
                if (!RefreshContact(contact,out var point,out var normal,out var motion)) {
                    contact.Surface=null;contact.Budget=0;continue;
                }
                LastContactPoint=point;LastContactNormal=normal;
                float strength=Mathf.Clamp01(motion.magnitude/Mathf.Max(1,fullIntensitySpeed));
                // Once a moving collision has started a scrape, maintain a dense
                // stream for the entire contact, including the slowdown on impact.
                contact.Budget+=scrapeEmission*Mathf.Lerp(.7f,1,strength)*Time.fixedDeltaTime;
                int count=Mathf.Min(32,Mathf.FloorToInt(contact.Budget));
                contact.Budget=Mathf.Min(contact.Budget-count,32);
                if (count == 0) continue;
                Emit(point,normal,motion,count,strength);
                ScrapeEmittedCount+=count;
            }
        }

        private void CreateParticles() {
            var child = new GameObject("Collision scrape sparks");
            child.transform.SetParent(transform,false);
            particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 82517;
            var main = particles.main;
            main.loop = true; main.playOnAwake = false; main.startSpeed = 0;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startSize3D = true; main.maxParticles = 768; main.gravityModifier = .8f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.25f));
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.55f,.2f),1)},
                new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(1,.45f),new GradientAlphaKey(0,1)});
            color.color = gradient;
            material = new Material(sparkMaterial);
            material.name = "Collision scrape sparks (runtime)";
            ConfigureBrightness();
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = .15f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        private float Between(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
        private void Emit(Vector3 point,Vector3 normal,Vector3 relativeMotion,int count,float strength) {
            if (particles == null) CreateParticles();
            ConfigureBrightness();
            Vector3 tangent = Vector3.ProjectOnPlane(relativeMotion,normal);
            Vector3 along = tangent.sqrMagnitude > .01f ? tangent.normalized : Vector3.Cross(normal,Vector3.up).normalized;
            Vector3 across = Vector3.Cross(normal,along).normalized;
            Vector3 carried = Vector3.ProjectOnPlane(body.GetPointVelocity(point),normal)*.8f;
            for (int i = 0; i < count; i++) {
                float width = sparkSize*Between(.65f,1.15f)*Mathf.Lerp(.8f,1.2f,strength);
                particles.Emit(new ParticleSystem.EmitParams {
                    position = point+normal*.012f+along*Between(-.09f,.09f),
                    velocity = carried-along*Between(8,15)+normal*outwardSpray*Between(.5f,1.3f)
                        +across*Between(-2,2)+Vector3.up*Between(.5f,2),
                    startLifetime = sparkLifetime*Between(.7f,1.2f),
                    startSize3D = new Vector3(width,width*streakLength*Between(.7f,1.2f),width),
                    startColor = sparkColor, rotation = Between(-85,85),
                    randomSeed = (uint)random.Next(1,int.MaxValue)
                },1);
                EmittedCount++;
            }
        }

        private void ConfigureBrightness() {
            if (material != null && !Mathf.Approximately(appliedBrightness,brightness)) {
                material.SetFloat("_Intensity",brightness);
                appliedBrightness = brightness;
            }
        }

        private void LateUpdate() {
            if (particles == null) return;
            ConfigureBrightness();
            bool pause = Time.timeScale <= 0 || (manager != null && manager.gameState == GameState.Pause);
            if (pause && !particles.isPaused) particles.Pause();
            else if (!pause && particles.isPaused) particles.Play();
        }

        private void OnDisable() {
            foreach (var contact in activeContacts) if (contact != null) { contact.Surface=null;contact.Budget=0; }
            if (particles != null) particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnEnable() { if (particles != null) particles.Play(); }
        private void OnDestroy() {
            if (particles != null) Destroy(particles.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
