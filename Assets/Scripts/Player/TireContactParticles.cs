using UnityEngine;
using Gameplay;

namespace CyberBiker.Player {
    public sealed class TireContactParticles : MonoBehaviour {
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Material smokeMaterial;
        [Tooltip("Smoke particle asset reused from the existing Cartoon FX explosion.")]
        [SerializeField] private ParticleSystem smokeTemplate;
        [SerializeField, Min(1)] private float referenceSpeed = 75;
        [SerializeField, Min(0)] private float steeringEmission = 90;
        [SerializeField, Range(0,1)] private float frontWheelAmount = 0.15f;
        [SerializeField, Range(0.02f,2f)] private float particleSize = 0.72f;
        [SerializeField, Range(0.2f,1.2f)] private float smokeLifetime = 0.34f;
        [SerializeField, Range(0,3)] private float smokeRise = 0.45f;
        [SerializeField, Min(0)] private float steeringSpraySpeed = 0.3f;
        [SerializeField, Range(0,1)] private float smokeOpacity = 0.8f;
        [Header("Lean activation")]
        [Tooltip("Actual motorcycle lean, in degrees, needed to start smoke and skid sparks.")]
        [SerializeField, Range(0,60)] private float startLeanAngle = 18;
        [Tooltip("Lean angle where the cloud emission reaches full strength.")]
        [SerializeField, Range(0,60)] private float fullLeanAngle = 35;
        [Tooltip("Stop this many degrees below the start angle to avoid flickering at the threshold.")]
        [SerializeField, Range(0,10)] private float leanReleaseMargin = 3;
        [Header("Skid sparks")]
        [SerializeField, Min(0)] private float skidSparkEmission = 45;
        [SerializeField, Range(0.01f,0.15f)] private float skidSparkSize = 0.08f;
        [Tooltip("Extra spark density at deep lean, on top of the normal lean response.")]
        [SerializeField, Range(1,5)] private float hardLeanSparkBoost = 2.5f;
        [SerializeField, Range(0.1f,0.4f)] private float skidSparkLifetime = 0.22f;
        [SerializeField, Range(1,5)] private float skidSparkStreakLength = 2.8f;
        [SerializeField] private LayerMask groundLayers = ~0;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly System.Random random = new System.Random(71459);
        private readonly ParticleSystem[] wheels = new ParticleSystem[2];
        private readonly ParticleSystem[] sparks = new ParticleSystem[2];
        private readonly Vector3[] previousContact = new Vector3[2];
        private readonly bool[] hadContact = new bool[2];
        private readonly float[] sparkBudgets = new float[2];
        private readonly float[] budgets = new float[2];
        private PlayerController player;
        private Rigidbody body;
        private Transform motorcycle;
        private GameplayManager manager;
        private bool leanActive;
        public float CurrentEmissionRate { get; private set; }
        public float CurrentSparkEmissionRate { get; private set; }
        public int EmittedCount { get; private set; }
        public int EmittedSparkCount { get; private set; }
        public int GroundedWheelCount { get; private set; }
        public int ParticleCount => Count(wheels) + Count(sparks);
        private static int Count(ParticleSystem[] systems) => (systems[0] != null ? systems[0].particleCount : 0) + (systems[1] != null ? systems[1].particleCount : 0);

        private void Awake() {
            player=GetComponent<PlayerController>(); body=GetComponent<Rigidbody>();
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
                if(filter.name=="Motorcycle") { motorcycle=filter.transform; break; }
            if(motorcycle==null || particleMaterial==null || smokeMaterial==null) { enabled=false; return; }
            for(int i=0;i<2;i++) {
                wheels[i]=CreateWheel(i==0 ? "Rear tire smoke" : "Front tire smoke",(uint)(71459+i),false);
                sparks[i]=CreateWheel(i==0 ? "Rear skid sparks" : "Front skid sparks",(uint)(71469+i),true);
            }
        }
        private ParticleSystem CreateWheel(string label,uint seed,bool spark) {
            ParticleSystem system;
            if(!spark && smokeTemplate!=null) {
                system=Instantiate(smokeTemplate,transform);system.name=label;
                system.transform.localPosition=Vector3.zero;system.transform.localRotation=Quaternion.identity;system.transform.localScale=Vector3.one;
            } else {
                var obj=new GameObject(label);obj.transform.SetParent(transform,false);system=obj.AddComponent<ParticleSystem>();
            }
            var go=system.gameObject;system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed=false;system.randomSeed=seed;
            var main=system.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=spark ? 64 : 256;main.startLifetime=.3f;main.startSpeed=0;main.gravityModifier=spark ? .7f : 0;main.useUnscaledTime=false;
            main.startSize3D=spark;main.startRotation3D=false;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.enabled=false;
            var velocity=system.velocityOverLifetime;velocity.enabled=false;
            var limit=system.limitVelocityOverLifetime;limit.enabled=false;
            var force=system.forceOverLifetime;force.enabled=false;
            var noise=system.noise;noise.enabled=false;
            var inherit=system.inheritVelocity;inherit.enabled=false;
            var color=system.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(spark ? 1 : .9f,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.95f,.65f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var size=system.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,spark ? 1f : .4f,1,spark ? .35f : 1.45f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();if(spark) renderer.sharedMaterial=particleMaterial;
            else {
                renderer.sharedMaterial=smokeMaterial;
                // Reuse the four Cartoon FX cloud silhouettes and their lobe normal map.
                // A fixed random atlas cell avoids flipbook popping while each cloud rolls.
                var data=system.customData;data.enabled=false;
                var sheet=system.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=2;
                sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.frameOverTime=0;
                sheet.startFrame=new ParticleSystem.MinMaxCurve(0f,1f);
                renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
                    ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV
                });
                renderer.pivot=new Vector3(0,-.24f,0);
                var spin=system.rotationOverLifetime;spin.enabled=true;spin.separateAxes=false;
                spin.z=new ParticleSystem.MinMaxCurve(-1.7f,1.7f);
                noise.enabled=true;noise.quality=ParticleSystemNoiseQuality.Low;noise.strength=.08f;
                noise.frequency=1.8f;noise.scrollSpeed=.6f;noise.damping=true;
            }
            // Explicit narrow spark billboards retain their streak shape when the
            // chase camera looks directly along their velocity, unlike stretched particles.
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.lengthScale=1.4f;renderer.velocityScale=0;
            renderer.cameraVelocityScale=0;renderer.maxParticleSize=spark ? .12f : .18f;
            renderer.sortMode=ParticleSystemSortMode.Distance;renderer.sortingFudge=0;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Play();return system;
        }
        private float Between(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
        private void LateUpdate() {
            if(manager==null) manager=FindAnyObjectByType<GameplayManager>();
            float dt=Time.deltaTime;
            if(dt<=0 || (manager!=null && manager.gameState==GameState.Pause)) {
                PauseSystems(wheels,true);PauseSystems(sparks,true);
                return;
            }
            PauseSystems(wheels,false);PauseSystems(sparks,false);
            CurrentEmissionRate=0;CurrentSparkEmissionRate=0;GroundedWheelCount=0;
            if(player==null || !player.IsAlive || (manager!=null && manager.gameState!=GameState.Playing)) { ResetEmission();return; }
            // Follow the visual bike's actual bank, rather than a sensitive steering input.
            float lean=Mathf.Abs(Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z));
            if(lean>=startLeanAngle) leanActive=true;
            else if(lean<=Mathf.Max(0,startLeanAngle-leanReleaseMargin)) leanActive=false;
            float leanAmount=leanActive ? Mathf.Lerp(.3f,1,Mathf.SmoothStep(0,1,
                Mathf.InverseLerp(startLeanAngle,Mathf.Max(startLeanAngle+.1f,fullLeanAngle),lean))) : 0;
            float speed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
            float pressure=Mathf.Pow(Mathf.Clamp01(speed/Mathf.Max(1,referenceSpeed)),1.2f);
            if(speed<3) { ResetEmission();return; }
            for(int i=0;i<2;i++) {
                Vector3 wheel=WheelContactPoint(i);
                int count=Physics.RaycastNonAlloc(wheel+Vector3.up*.6f,Vector3.down,hits,1f,groundLayers,QueryTriggerInteraction.Ignore);
                float nearest=float.MaxValue;int ground=-1;
                for(int h=0;h<count;h++)
                    if(!hits[h].transform.IsChildOf(transform) && hits[h].normal.y>.4f && hits[h].distance<nearest) {ground=h;nearest=hits[h].distance;}
                if(ground<0) { budgets[i]=sparkBudgets[i]=0;hadContact[i]=false;continue; }
                GroundedWheelCount++;
                if(!leanActive) { budgets[i]=sparkBudgets[i]=0;hadContact[i]=false;continue; }
                float rate=pressure*leanAmount*steeringEmission*(i==0 ? 1 : frontWheelAmount);
                CurrentEmissionRate+=rate;budgets[i]+=rate*Mathf.Min(dt,.05f);
                int emit=Mathf.Min(32,Mathf.FloorToInt(budgets[i]));budgets[i]-=emit;
                Vector3 normal=hits[ground].normal;
                float lateral=Vector3.Dot(body.linearVelocity,transform.right);
                // Dust lags behind the actual sideways slip; do not force all
                // particles to a chosen side just because a steering key is held.
                Vector3 slip=-transform.right*Mathf.Clamp(lateral/8,-1,1)*steeringSpraySpeed;
                Vector3 contact=hits[ground].point+normal*.07f;
                bool continuousContact=hadContact[i] && Vector3.Distance(previousContact[i],contact)<10;
                Vector3 from=continuousContact ? previousContact[i] : contact;
                // Fill the space travelled since the last frame, instead of spawning
                // separate frame-sized clusters at a fast-moving point.
                for(int j=0;j<emit;j++) {
                    float shade=Between(.66f,.8f);
                    float fraction=(j+.5f)/Mathf.Max(1,emit);
                    Vector3 cloudVelocity=Vector3.ProjectOnPlane(body.linearVelocity,normal)*Between(.94f,.96f)
                        +slip+normal*smokeRise*Between(.7f,1.2f)+transform.right*Between(-.12f,.12f);
                    // Earlier births have already travelled with the bike during this
                    // frame. Compensate that motion so the trail stays attached even at low FPS.
                    float age=continuousContact ? (1-fraction)*Mathf.Min(dt,.05f) : 0;
                    var particle=new ParticleSystem.EmitParams {
                        position=Vector3.Lerp(from,contact,fraction)+cloudVelocity*age+transform.right*Between(-.025f,.025f),
                        velocity=cloudVelocity,
                        startLifetime=smokeLifetime*Between(.85f,1.15f),startSize=particleSize*Between(.8f,1.15f),
                        startColor=new Color(shade*.86f,shade*.94f,shade,smokeOpacity),
                        rotation=Between(0,360),randomSeed=(uint)random.Next(1,int.MaxValue)
                    };
                    wheels[i].Emit(particle,1);EmittedCount++;
                }
                previousContact[i]=contact;hadContact[i]=true;
                float skid=leanAmount*Mathf.Lerp(.5f,1,Mathf.Clamp01(Mathf.Abs(lateral)/6));
                float hardLean=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,leanAmount));
                float sparkRate=pressure*skid*skidSparkEmission*Mathf.Lerp(1,hardLeanSparkBoost,hardLean)*(i==0 ? 1 : frontWheelAmount);
                CurrentSparkEmissionRate+=sparkRate;
                sparkBudgets[i]+=sparkRate*Mathf.Min(dt,.05f);
                int flecks=Mathf.Min(8,Mathf.FloorToInt(sparkBudgets[i]));sparkBudgets[i]-=flecks;
                float sparkSpread=Mathf.Lerp(.8f,4.2f,hardLean);
                for(int j=0;j<flecks;j++) {
                    float sparkWidth=skidSparkSize*Between(.85f,1.15f)*Mathf.Lerp(1,1.25f,hardLean);
                    var particle=new ParticleSystem.EmitParams {
                        position=hits[ground].point+normal*.06f,
                        velocity=Vector3.ProjectOnPlane(body.linearVelocity,normal)*Between(.82f,.88f)+slip
                            +normal*Between(1.2f,2.2f)+transform.right*Between(-sparkSpread,sparkSpread),
                        startLifetime=skidSparkLifetime*Between(.85f,1.15f),
                        startSize3D=new Vector3(sparkWidth,sparkWidth*skidSparkStreakLength*1.5f,sparkWidth),
                        rotation=Between(-75,75),
                        startColor=new Color(1,.65f,.22f,1),randomSeed=(uint)random.Next(1,int.MaxValue)
                    };
                    sparks[i].Emit(particle,1);EmittedCount++;EmittedSparkCount++;
                }
            }
        }
        private Vector3 WheelContactPoint(int wheel) => motorcycle.TransformPoint(new Vector3(0,.02f,wheel==0 ? -.5296f : 1.4493f));
        private void ResetEmission() {
            leanActive=false;hadContact[0]=hadContact[1]=false;
            budgets[0]=budgets[1]=sparkBudgets[0]=sparkBudgets[1]=0;
        }
        private static void PauseSystems(ParticleSystem[] systems,bool pause) {
            foreach(var system in systems) if(system!=null) {
                if(pause && !system.isPaused) system.Pause();
                else if(!pause && system.isPaused) system.Play();
            }
        }
        private void OnEnable() {
            foreach(var system in wheels) if(system!=null) system.Play();
            foreach(var system in sparks) if(system!=null) system.Play();
        }
        private void OnDisable() {
            CurrentEmissionRate=0;CurrentSparkEmissionRate=0;ResetEmission();
            foreach(var system in wheels) if(system!=null) system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var system in sparks) if(system!=null) system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDestroy() {
            foreach(var system in wheels) if(system!=null) Destroy(system.gameObject);
            foreach(var system in sparks) if(system!=null) Destroy(system.gameObject);
        }
    }
}
