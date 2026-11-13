using System.Collections.Generic;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Player {
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class SwordFinisherEffects : MonoBehaviour {
        [Header("Blade trail")]
        [SerializeField] private Material ribbonMaterial;
        [Tooltip("Blade coordinates measured from Sword_Main; the blade extends along negative Y.")]
        [SerializeField] private Vector3 bladeBase = new Vector3(0,-.0015f,0);
        [SerializeField] private Vector3 bladeTip = new Vector3(0,-.0092f,0);
        [SerializeField, Range(.05f,.35f)] private float trailLifetime = .16f;
        [Header("Dash wind around the rider")]
        [SerializeField, Range(12,40)] private int windRibbonCount = 26;
        [SerializeField, Range(.01f,.08f)] private float windRibbonWidth = .028f;
        [SerializeField, Range(.1f,1)] private float windOpacity = .4f;
        [SerializeField, Range(1,6)] private float windFlowSpeed = 3.6f;
        [SerializeField, Range(.15f,.7f)] private float dashTimeScale = .35f;
        [SerializeField, Range(.3f,1.2f)] private float dashSlowDuration = .65f;
        [SerializeField, ColorUsage(true,true)] private Color trailColor = new Color(.3f,1.5f,2.8f,.8f);
        [Header("Impact")]
        [SerializeField] private ParticleSystem hitPrefab;
        [SerializeField, Range(.2f,3)] private float hitScale = 1.2f;
        [Header("Contact contrast and slash flash")]
        [SerializeField] private Shader impactScreenShader;
        [SerializeField, Range(0,.8f)] private float impactDarkening = .5f;
        [SerializeField, Range(0,1)] private float impactDesaturation = .55f;
        [SerializeField, Range(0,1)] private float slashFlash = .65f;
        [SerializeField, Range(.02f,.1f)] private float flashDuration = .045f;
        [SerializeField, Range(.1f,.6f)] private float contrastDuration = .32f;
        [Header("Sword sound layers - downloaded recordings")]
        [SerializeField] private AudioClip[] swordSwishes;
        [SerializeField] private AudioClip swordMetalHit;
        [SerializeField] private AudioClip swordLowImpact;
        [SerializeField, Range(0,2)] private float swishVolume = .85f;
        [SerializeField, Range(0,2)] private float metalHitVolume = 1.1f;
        [SerializeField, Range(0,2)] private float lowImpactVolume = .75f;
        [SerializeField, Range(0,15)] private float impactDuckDb = 9;
        [SerializeField, Range(.1f,.7f)] private float impactDuckRecovery = .32f;
        [Header("Explosion sound layers")]
        [SerializeField] private AudioClip explosionBlast;
        [SerializeField] private AudioClip explosionBass;
        [SerializeField] private AudioClip explosionDebris;
        [SerializeField, Range(0,2)] private float explosionBlastVolume = .9f;
        [SerializeField, Range(0,2)] private float explosionBassVolume = .65f;
        [SerializeField, Range(0,1)] private float explosionDebrisVolume = .25f;
        [Header("Finishing camera")]
        [SerializeField, Range(0,20)] private float swingZoom = 2;
        [Tooltip("FOV change during the hit. Negative values widen the view; positive values tighten it.")]
        [SerializeField, Range(-20,25)] private float hitZoom = -8;
        [SerializeField, Range(0,1)] private float cameraDolly = .2f;
        [SerializeField, Range(0,3)] private float aftermathPullback = 1.1f;
        [SerializeField, Range(0,1)] private float aftermathCameraLift = .35f;
        [SerializeField, Range(.5f,3)] private float aftermathCameraDuration = 1.5f;
        [SerializeField, Range(0,2)] private float cameraSideShift = .8f;
        [Header("Hit side view")]
        [Tooltip("Additional sideways travel toward the sword side, held through the explosion.")]
        [SerializeField, Range(0,3)] private float hitCameraSideShift = 1.25f;
        [SerializeField, Range(.08f,.6f)] private float hitCameraSideMoveTime = .18f;
        [SerializeField, Range(.15f,1.2f)] private float hitCameraSideReturnTime = .55f;
        [SerializeField, Range(0,20)] private float hitCameraLookTurn = 12;
        [SerializeField, Range(0,8)] private float targetFramingTurn = 5;
        [SerializeField, Range(0,6)] private float cameraRoll = 2;
        [SerializeField, Range(0,.3f)] private float hitShake = .075f;
        [SerializeField, Min(.1f)] private float hitCameraDuration = .42f;
        [Header("Brief slow-motion cut")]
        [Tooltip("Enable world slow motion for dash, hit and explosion. Animation playback speed and contact pose hold are separate controls.")]
        [SerializeField] private bool slowMotionEnabled=true;
        public bool SlowMotionEnabled {get=>slowMotionEnabled;set{slowMotionEnabled=value;if(!value)gameplay?.CancelCombatSlowMotion();}}
        [SerializeField, Range(.1f,1)] private float hitTimeScale = .2f;
        [SerializeField, Range(0,1.5f)] private float hitSlowDuration = .55f;
        [Tooltip("Real seconds spent smoothly returning from slow motion to normal speed.")]
        [SerializeField, Range(.1f,1.5f)] private float slowMotionRecovery = .65f;
        [Header("Explosion momentum and slow motion")]
        [SerializeField, Range(.15f,.7f)] private float explosionTimeScale = .28f;
        [SerializeField, Range(.2f,2)] private float explosionSlowDuration = .9f;
        [SerializeField, Range(0,4)] private float explosionDrag = 1.6f;
        [Header("Smoke carried out of the explosion")]
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private ParticleSystem smokeTemplate;
        [SerializeField, Range(.5f,3)] private float smokeWakeDuration = 1.6f;
        [SerializeField, Range(.6f,2)] private float smokeCloudSize = 1.25f;
        [SerializeField, Range(60,220)] private float smokeDensity = 140;
        [SerializeField, Range(3,10)] private float smokeBlastRadius = 6;
        public int ImpactCount { get; private set; }
        public int CarriedExplosionCount { get; private set; }
        public Vector3 LastExplosionVelocity { get; private set; }
        public int SmokeWakeCount { get; private set; }
        public int SmokeParticleCount => smokeWake!=null ? smokeWake.particleCount : 0;
        public int LastImpactParticleCount { get; private set; }
        public int TrailVertexCount => mesh!=null ? mesh.vertexCount : 0;
        public float CameraAmount => anticipation+hitEnvelope+aftermathEnvelope+Mathf.Abs(cameraZoom)*.01f+Mathf.Abs(cameraSide);
        public float LastCameraZoom { get; private set; }
        public float LastCameraSideOffset { get; private set; }
        public int VisibleWindRibbons {get {int count=0;foreach(var line in windRibbons)if(line!=null && line.enabled)count++;return count;}}
        private struct BladeSample {public Vector3 bottom,top;public float time;}
        private readonly List<BladeSample> samples = new List<BladeSample>(64);
        private readonly List<Vector3> vertices = new List<Vector3>(128);
        private readonly List<Color> colors = new List<Color>(128);
        private readonly List<Vector2> uvs = new List<Vector2>(128);
        private readonly List<int> triangles = new List<int>(384);
        private BikeSwordCombat combat;
        private GameplayManager gameplay;
        private Mesh mesh;
        private GameObject ribbonObject;
        private float previousProgress,anticipation,hitAge=100,hitEnvelope,aftermathAge=100,aftermathEnvelope;
        private bool wasSwinging;
        private bool swishPlayed;
        private SwordImpactScreen screenEffect;
        private Vector3 hitPosition;
        private float sideSign=1;
        private readonly List<LineRenderer> windRibbons=new List<LineRenderer>();
        private float windAmount,windClock;
        private float cameraZoom,cameraTravel,cameraLift,cameraFrame,trailClock;
        private float cameraSide,cameraSideVelocity;
        private Transform framingBlast;
        private Vector3 impactTravel;
        private ParticleSystem smokeWake;
        private Transform pendingBlast;
        private Rigidbody bikeBody;
        private float smokeUntil,smokeBudget;
        private Vector3 previousSmokePosition;
        private bool hadSmokePosition;
        private readonly System.Random smokeRandom=new System.Random(82273);
        private void Start() {
            combat=GetComponent<BikeSwordCombat>();gameplay=FindAnyObjectByType<GameplayManager>();
            bikeBody=GetComponent<Rigidbody>();
            ribbonObject=new GameObject("Sword blade ribbon",typeof(MeshFilter),typeof(MeshRenderer));ribbonObject.transform.SetParent(transform,false);
            mesh=new Mesh{name="Sword blade history"};mesh.MarkDynamic();ribbonObject.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=ribbonObject.GetComponent<MeshRenderer>();renderer.sharedMaterial=ribbonMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            for(int i=0;i<windRibbonCount;i++) {
                var go=new GameObject("Dash wind curl "+i);go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=12;line.sharedMaterial=ribbonMaterial;line.widthMultiplier=windRibbonWidth+(i%4)*.005f;line.numCapVertices=3;line.numCornerVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,.7f),new Keyframe(.55f,1),new Keyframe(1,0));
                line.enabled=false;windRibbons.Add(line);
            }
            CreateSmokeWake();
        }
        private void CreateSmokeWake() {
            if(smokeMaterial==null)return;
            smokeWake=smokeTemplate!=null ? Instantiate(smokeTemplate,transform) : new GameObject("Explosion smoke wake").AddComponent<ParticleSystem>();
            smokeWake.transform.SetParent(transform,false);smokeWake.name="Explosion smoke wake";smokeWake.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smokeWake.main;main.loop=true;main.startSize3D=false;main.startRotation3D=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.maxParticles=512;main.gravityModifier=0;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=smokeWake.emission;emission.enabled=false;var shape=smokeWake.shape;shape.enabled=false;
            var limit=smokeWake.limitVelocityOverLifetime;limit.enabled=false;var velocity=smokeWake.velocityOverLifetime;velocity.enabled=false;var force=smokeWake.forceOverLifetime;force.enabled=false;var inherit=smokeWake.inheritVelocity;inherit.enabled=false;
            var collision=smokeWake.collision;collision.enabled=false;var triggers=smokeWake.trigger;triggers.enabled=false;var data=smokeWake.customData;data.enabled=false;
            var size=smokeWake.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.35f,1.2f),new Keyframe(1,1.6f)));
            var color=smokeWake.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.06f),new GradientAlphaKey(.75f,.7f),new GradientAlphaKey(0,1)});color.color=gradient;
            var sheet=smokeWake.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=2;sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.frameOverTime=0;sheet.startFrame=new ParticleSystem.MinMaxCurve(0,1);
            var spin=smokeWake.rotationOverLifetime;spin.enabled=true;spin.z=new ParticleSystem.MinMaxCurve(-1.4f,1.4f);
            var noise=smokeWake.noise;noise.enabled=true;noise.strength=.1f;noise.frequency=1.3f;noise.scrollSpeed=.6f;
            var renderer=smokeWake.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sharedMaterial=smokeMaterial;renderer.maxParticleSize=.24f;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});smokeWake.Play();
        }
        private void UpdateSmokeWake() {
            if(smokeWake==null)return;
            if(pendingBlast!=null && Vector3.Distance(transform.position,pendingBlast.position)<smokeBlastRadius){smokeUntil=Time.time+smokeWakeDuration;pendingBlast=null;SmokeWakeCount++;}
            var rear=transform.TransformPoint(new Vector3(0,.45f,-.7f));
            if(Time.time<smokeUntil) {
                smokeBudget+=Time.deltaTime*smokeDensity;int count=Mathf.FloorToInt(smokeBudget);smokeBudget-=count;
                var inherited=bikeBody!=null ? bikeBody.linearVelocity-Vector3.forward*10 : Vector3.zero;
                for(int i=0;i<count;i++) {
                    float t=(i+.5f)/Mathf.Max(1,count),age=Time.deltaTime*(1-t);float random=(float)smokeRandom.NextDouble();
                    var emit=new ParticleSystem.EmitParams{position=(hadSmokePosition ? Vector3.Lerp(previousSmokePosition,rear,t) : rear)+inherited*age,
                        velocity=inherited+new Vector3((random-.5f)*.5f,.3f,0),startLifetime=.55f+random*.12f,startSize=smokeCloudSize*(.85f+random*.3f),rotation=random*360,startColor=new Color(.4f,.44f,.5f,.9f)};
                    smokeWake.Emit(emit,1);
                }
            }
            previousSmokePosition=rear;hadSmokePosition=true;
        }
        public void BeginSwing(){samples.Clear();previousProgress=0;wasSwinging=true;swishPlayed=false;windClock=0;if(slowMotionEnabled)gameplay?.BeginCombatSlowMotion(dashTimeScale,dashSlowDuration);}
        public void PlayHit(Vector3 position,Vector3 velocity,BikeSwordCombat.AttackSide side) {
            ImpactCount++;hitAge=0;aftermathAge=0;hitPosition=position;impactTravel=velocity;framingBlast=null;sideSign=side==BikeSwordCombat.AttackSide.Left ? -1 : 1;
            if(hitPrefab!=null) {
                var effect=Instantiate(hitPrefab,position,Quaternion.LookRotation(Camera.main!=null ? -Camera.main.transform.forward : Vector3.back));
                effect.name="Sword cut impact";effect.transform.localScale=Vector3.one*hitScale;
                foreach(var system in effect.GetComponentsInChildren<ParticleSystem>(true)) {
                    var main=system.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                }
                var travel=effect.gameObject.AddComponent<SwordImpactTravel>();travel.Initialize(velocity,gameplay);
                effect.Simulate(1f/60f,true,true,false);effect.Play(true);
                LastImpactParticleCount=0;foreach(var system in effect.GetComponentsInChildren<ParticleSystem>())LastImpactParticleCount+=system.particleCount;
            }
            if(slowMotionEnabled)gameplay?.BeginCombatSlowMotion(hitTimeScale,hitSlowDuration,slowMotionRecovery);
            var audio=CyberBiker.Audio.GameAudio.Active;
            if(audio!=null) {
                audio.DuckForSwordImpact(impactDuckDb,.09f,impactDuckRecovery);
                audio.PlaySwordSound(swordMetalHit,position,transform,metalHitVolume,1);
                audio.PlaySwordSound(swordLowImpact,position,transform,lowImpactVolume,.82f);
            }
        }
        public void CarryExplosion(ParticleSystem explosion,Vector3 velocity) {
            CarriedExplosionCount++;LastExplosionVelocity=velocity;
            pendingBlast=explosion.transform;
            framingBlast=explosion.transform;aftermathAge=Mathf.Min(aftermathAge,.1f);
            foreach(var system in explosion.GetComponentsInChildren<ParticleSystem>(true)){var main=system.main;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;}
            var travel=explosion.gameObject.AddComponent<SwordImpactTravel>();travel.Initialize(velocity,gameplay,explosionDrag);
            explosion.Simulate(1f/60f,true,true,false);explosion.Play(true);
            if(slowMotionEnabled && combat!=null && combat.AllowsAftermathSlowMotion)gameplay?.BeginCombatSlowMotion(explosionTimeScale,explosionSlowDuration,slowMotionRecovery);
            var audio=CyberBiker.Audio.GameAudio.Active;
            if(audio!=null) {
                audio.DuckForSwordImpact(impactDuckDb,.12f,.55f);
                audio.PlaySwordExplosion(explosionBlast,explosion.transform,explosionBlastVolume,1);
                audio.PlaySwordExplosion(explosionBass,explosion.transform,explosionBassVolume,.9f);
                audio.PlaySwordExplosion(explosionDebris,explosion.transform,explosionDebrisVolume,.85f);
            }
        }
        private void Update() {
            if(combat==null || gameplay==null)return;
            if(!slowMotionEnabled && gameplay.CombatSlowMotionRemaining>0)gameplay.CancelCombatSlowMotion();
            if(gameplay.gameState!=GameState.Playing) {
                if(gameplay.gameState!=GameState.Pause){anticipation=hitEnvelope=aftermathEnvelope=cameraZoom=cameraTravel=cameraLift=cameraFrame=cameraSide=cameraSideVelocity=windAmount=0;smokeUntil=0;hitAge=aftermathAge=100;screenEffect?.Clear();foreach(var line in windRibbons)line.enabled=false;}
                return;
            }
            float dt=FinisherClock.UnscaledDeltaTime;trailClock+=dt;
            if(!swishPlayed && combat.Phase==BikeSwordCombat.CombatPhase.Striking && combat.SwingProgress>=.36f) {
                swishPlayed=true;
                if(swordSwishes!=null && swordSwishes.Length>0)CyberBiker.Audio.GameAudio.Active?.PlaySwordSound(swordSwishes[ImpactCount%swordSwishes.Length],transform.position,transform,swishVolume,.94f);
            }
            float desired=combat.Phase==BikeSwordCombat.CombatPhase.Striking ? 1 : combat.IsAssisting ? .25f : 0;
            anticipation=Mathf.Lerp(anticipation,desired,1-Mathf.Exp(-dt*12));
            windAmount=Mathf.MoveTowards(windAmount,combat.IsDashing ? 1 : 0,dt/(combat.IsDashing ? .045f : .16f));windClock+=dt;
            for(int i=0;i<windRibbons.Count;i++) {
                var line=windRibbons[i];line.enabled=windAmount>.005f;if(!line.enabled)continue;
                float seed=i*.618034f;float cycle=Mathf.Repeat(seed+windClock*(windFlowSpeed+(i%3)*.2f),1);
                float radius=.65f+(i%5)*.13f;float angle=i*2.399963f+windClock*(3.5f+(i%4)*.4f);
                float z=Mathf.Lerp(2.2f,-2.3f,cycle);float arc=.65f+(i%4)*.22f;
                for(int j=0;j<12;j++) {
                    float t=j/11f;float theta=angle+t*arc;
                    line.SetPosition(j,new Vector3(Mathf.Cos(theta)*radius,1.05f+Mathf.Sin(theta)*radius*.8f,z-t*(.65f+(i%3)*.2f)));
                }
                float fade=Mathf.Sin(cycle*Mathf.PI)*windAmount;
                var color=new Color(.7f,1.1f,1.3f,windOpacity*fade);line.startColor=color;line.endColor=new Color(.5f,.8f,1,0);
            }
            hitAge+=dt;aftermathAge+=dt;hitPosition+=impactTravel*Time.deltaTime;
            float pulse=Mathf.Clamp01(hitAge/Mathf.Max(.1f,hitCameraDuration));
            hitEnvelope=hitAge<hitCameraDuration ? Mathf.Sin(Mathf.Min(1,pulse/.12f)*Mathf.PI*.5f)*Mathf.Pow(1-pulse,2) : 0;
            float aftermathFade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,Mathf.Max(.6f,aftermathCameraDuration),aftermathAge));
            aftermathEnvelope=Mathf.SmoothStep(0,1,aftermathAge/.1f)*aftermathFade;
            float desiredZoom=anticipation*(combat.IsDashing ? -4 : swingZoom)+aftermathEnvelope*hitZoom;
            float response=1-Mathf.Exp(-dt*8);
            cameraZoom=Mathf.Lerp(cameraZoom,desiredZoom,response);
            cameraTravel=Mathf.Lerp(cameraTravel,(combat.IsDashing ? -1 : 1)*cameraDolly*anticipation-aftermathPullback*aftermathEnvelope,response);
            cameraLift=Mathf.Lerp(cameraLift,aftermathCameraLift*aftermathEnvelope,response);
            cameraFrame=Mathf.Lerp(cameraFrame,Mathf.Max(anticipation,aftermathEnvelope*.65f),response);
            float swingSide=combat.LastAttackSide==BikeSwordCombat.AttackSide.Left ? -1 : 1;
            float desiredSide=swingSide*cameraSideShift*anticipation+sideSign*hitCameraSideShift*aftermathEnvelope;
            float sideResponse=anticipation>.1f || aftermathEnvelope>.02f ? hitCameraSideMoveTime : hitCameraSideReturnTime;
            // Smooth the signed offset itself, including left/right chain attacks.
            // The ordinary steering and aiming camera remain separate.
            cameraSide=Mathf.SmoothDamp(cameraSide,desiredSide,ref cameraSideVelocity,sideResponse,Mathf.Infinity,dt);
        }
        private void LateUpdate() {
            if(combat==null || mesh==null || gameplay.gameState!=GameState.Playing || Time.deltaTime<=0)return;
            UpdateSmokeWake();
            bool swinging=combat.Phase==BikeSwordCombat.CombatPhase.Striking;
            float progress=combat.SwingProgress;
            if(swinging) {
                if(!wasSwinging)BeginSwing();
                float start=Mathf.Max(.18f,previousProgress);float end=Mathf.Min(.85f,progress);
                if(end>=start) {
                    int count=Mathf.Clamp(Mathf.CeilToInt((end-start)*combat.SwingDuration/.015f),1,20);
                    for(int i=1;i<=count;i++) {
                        float fraction=Mathf.Lerp(start,end,i/(float)count);
                        combat.SampleBlade(fraction,bladeBase,bladeTip,out var bottom,out var top);
                        samples.Add(new BladeSample{bottom=bottom,top=top,time=trailClock-(end-fraction)*combat.SwingRealDuration});
                    }
                }
                previousProgress=progress;
            }
            wasSwinging=swinging;
            // Once the last trail segment has faded, its empty mesh is already
            // clear. Do not rebuild and upload that empty mesh every idle frame.
            if(samples.Count==0 && mesh.vertexCount==0)return;
            samples.RemoveAll(s=>trailClock-s.time>trailLifetime);
            while(samples.Count>64)samples.RemoveAt(0);
            vertices.Clear();colors.Clear();uvs.Clear();triangles.Clear();
            for(int i=0;i<samples.Count;i++) {
                var sample=samples[i];float alpha=Mathf.Pow(Mathf.Clamp01(1-(trailClock-sample.time)/trailLifetime),1.3f);
                vertices.Add(sample.bottom);vertices.Add(sample.top);
                var inner=trailColor;inner.a*=alpha*.15f;var outer=trailColor;outer.a*=alpha;
                colors.Add(inner);colors.Add(outer);uvs.Add(new Vector2(i/(float)Mathf.Max(1,samples.Count-1),0));uvs.Add(new Vector2(i/(float)Mathf.Max(1,samples.Count-1),1));
                if(i==0)continue;int n=i*2;triangles.Add(n-2);triangles.Add(n-1);triangles.Add(n);triangles.Add(n);triangles.Add(n-1);triangles.Add(n+1);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        public void ApplyCamera(Camera view) {
            if(combat==null || view==null)return;
            if(impactScreenShader!=null) {
                if(screenEffect==null || screenEffect.gameObject!=view.gameObject)screenEffect=view.GetComponent<SwordImpactScreen>() ?? view.gameObject.AddComponent<SwordImpactScreen>();
                float contrast=hitAge<contrastDuration ? Mathf.Pow(1-hitAge/contrastDuration,1.4f) : 0;
                float flash=hitAge<flashDuration ? Mathf.Pow(1-hitAge/flashDuration,2) : 0;
                var point=view.WorldToViewportPoint(hitPosition);
                var center=point.z>0 ? new Vector2(Mathf.Clamp(point.x,.15f,.85f),Mathf.Clamp(point.y,.2f,.8f)) : new Vector2(.5f,.5f);
                screenEffect.Configure(impactScreenShader,impactDarkening*contrast,impactDesaturation*contrast,slashFlash*flash,center,sideSign);
            }
            if(CameraAmount<.0001f){LastCameraZoom=LastCameraSideOffset=0;return;}
            float zoom=cameraZoom;
            LastCameraZoom=zoom;
            view.fieldOfView=Mathf.Clamp(view.fieldOfView-zoom,38,110);
            view.transform.position+=view.transform.forward*cameraTravel+Vector3.up*cameraLift;
            float side=combat.LastAttackSide==BikeSwordCombat.AttackSide.Left ? -1 : 1;
            LastCameraSideOffset=cameraSide;
            view.transform.position+=view.transform.right*cameraSide;
            var playerFocus=transform.position+Vector3.up*2;
            var blastFocus=framingBlast!=null ? framingBlast.position+Vector3.up : hitPosition;
            // Frame the rider and nearby blast together without chasing an old
            // explosion behind the bike or turning toward the next distant enemy.
            blastFocus=playerFocus+Vector3.ClampMagnitude(blastFocus-playerFocus,8);
            var focus=aftermathEnvelope>.1f ? Vector3.Lerp(playerFocus,blastFocus,.3f) : combat.Target!=null ? Vector3.Lerp(playerFocus,combat.Target.transform.position+Vector3.up*2,.55f) : playerFocus;
            var direction=focus-view.transform.position;
            float framingTurn=Mathf.Max(targetFramingTurn*cameraFrame,hitCameraLookTurn*aftermathEnvelope);
            if(direction.sqrMagnitude>.01f)view.transform.rotation=Quaternion.RotateTowards(view.transform.rotation,Quaternion.LookRotation(direction),framingTurn);
            view.transform.rotation*=Quaternion.Euler(0,0,side*cameraRoll*anticipation);
            if(hitEnvelope>0) {
                float kick=Mathf.Sin(Mathf.Clamp01(hitAge/.2f)*Mathf.PI)*Mathf.Exp(-hitAge*7);
                view.transform.position+=(view.transform.right*sideSign*.5f+view.transform.up*.4f-view.transform.forward*1.6f)*kick*hitShake;
                float shake=Mathf.Sin(hitAge*90)*Mathf.Exp(-hitAge*14)*hitEnvelope;
                view.transform.rotation*=Quaternion.Euler(-2.4f*kick+shake*.6f,sideSign*1.0f*kick,sideSign*1.3f*kick+shake*.5f);
            }
        }
        private void OnDisable(){screenEffect?.Clear();samples.Clear();if(mesh!=null)mesh.Clear();if(smokeWake!=null)smokeWake.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);foreach(var line in windRibbons)if(line!=null)line.enabled=false;windAmount=0;anticipation=hitEnvelope=aftermathEnvelope=cameraZoom=cameraTravel=cameraLift=cameraFrame=cameraSide=cameraSideVelocity=0;LastCameraSideOffset=0;hitAge=aftermathAge=100;}
        private void OnDestroy(){if(screenEffect!=null)Destroy(screenEffect);if(mesh!=null)Destroy(mesh);if(ribbonObject!=null)Destroy(ribbonObject);}
    }
    public sealed class SwordImpactTravel : MonoBehaviour {
        private Vector3 velocity;private GameplayManager gameplay;private float age,drag;
        public void Initialize(Vector3 movement,GameplayManager manager,float damping=0){velocity=movement;gameplay=manager;drag=damping;}
        private void LateUpdate(){if(gameplay==null || gameplay.gameState!=GameState.Playing)return;age+=Time.deltaTime;transform.position+=velocity*Time.deltaTime;velocity*=Mathf.Exp(-drag*Time.deltaTime);if(age>1.5f)Destroy(gameObject);}
    }
}

