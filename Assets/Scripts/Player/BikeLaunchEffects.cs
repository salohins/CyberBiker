using System.Collections.Generic;
using CyberBiker.Audio;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Player {
    // Presentation only. BikeSwordCombat continues to own boost, timing and targeting.
    [DefaultExecutionOrder(350),DisallowMultipleComponent]
    public sealed class BikeLaunchEffects : MonoBehaviour {
        [Header("Launch transition - real seconds")]
        [SerializeField,Range(.05f,1)] private float entryTime=.18f;
        [SerializeField,Range(.1f,2)] private float exitTime=.5f;
        [Header("Launch engine and wind - downloaded recordings")]
        [SerializeField] private AudioClip powerEngineLoop;
        [SerializeField,Range(0,1)] private float engineLayerVolume=.65f;
        [SerializeField,Range(.7f,1.5f)] private float engineLayerPitch=1.12f;
        [SerializeField,Range(0,6)] private float engineGainDb=2;
        [SerializeField,Range(0,6)] private float windGainDb=2.5f;
        [SerializeField] private AudioClip exhaustCrack;
        [SerializeField,Range(0,2)] private float exhaustVolume=1.35f;
        [Header("Launch camera")]
        [SerializeField,Range(-20,25)] private float fovIncrease=7;
        [SerializeField] private Vector3 cameraViewOffset,cameraViewEuler;
        [SerializeField] private Shader borderShader;
        [SerializeField,Range(0,1)] private float borderGlow=.32f;
        [SerializeField,Range(.01f,.15f)] private float borderWidth=.045f;
        [SerializeField,ColorUsage(true,false)] private Color borderColor=new Color(.06f,.28f,1,1);
        [Header("Launch wind particles")]
        [SerializeField,HideInInspector] private Material flowMaterial;
        [SerializeField] private ParticleSystem windParticleTemplate;
        [SerializeField] private Material windParticleMaterial;
        [SerializeField,Range(0,6)] private float windParticleBrightness=3;
        [SerializeField,Range(0,1500)] private float windEmissionRate=900;
        [SerializeField,Range(.05f,.8f)] private float windParticleLifetime=.24f;
        [SerializeField,Range(.01f,.1f)] private float windParticleWidth=.012f;
        [SerializeField,Range(.1f,2)] private float windParticleLength=.55f;
        [SerializeField,Range(1,25)] private float windParticleSpeed=10;
        [SerializeField,Range(.1f,2)] private float windRadius=.35f;
        [SerializeField,Range(.5f,1)] private float windRearFraction=1;
        [Tooltip("Centre of the wind emitter behind the rear wheel, in player-local coordinates.")]
        [SerializeField] private Vector3 windRearOffset=new Vector3(0,.38f,-2);
        [SerializeField] private Color windParticleColor=new Color(.015f,.2f,1,.7f);
        [Header("Launch wind around the jacket")]
        [SerializeField] private Transform[] jacketWindAnchors;
        [Tooltip("Small offset below/back from each animated hem tip, in player-local units.")]
        [SerializeField] private Vector3 jacketWindOffset=new Vector3(0,-.035f,-.07f);
        [SerializeField,Range(0,400)] private float jacketWindEmissionRate=100;
        [Tooltip("Additional airflow above the waving hem; does not reduce the lower airflow.")]
        [SerializeField,Range(0,400)] private float jacketWindUpperEmissionRate=60;
        [SerializeField] private Vector3 jacketWindUpperOffset=new Vector3(0,.09f,-.04f);
        [SerializeField,Range(.05f,.4f)] private float jacketWindLifetime=.18f;
        [SerializeField,Range(.003f,.03f)] private float jacketWindWidth=.008f;
        [SerializeField,Range(.1f,1)] private float jacketWindLength=.4f;
        [SerializeField,Range(1,15)] private float jacketWindSpeed=8;
        [SerializeField,Range(0,6)] private float jacketWindBrightness=2.8f;
        [SerializeField,Range(0,1)] private float jacketWindOpacity=.65f;
        [Header("Exhaust flames - motorcycle local coordinates")]
        [SerializeField] private Transform motorcycle;
        [SerializeField] private Material flameMaterial;
        [Tooltip("Centres of the three actual right-hand exhaust outlets, measured from the mesh.")]
        [SerializeField] private Vector3[] exhaustPorts={new Vector3(.2753f,.8402f,-.754f),new Vector3(.2753f,.9227f,-.720f),new Vector3(.2753f,1.0053f,-.686f)};
        [SerializeField] private Vector3 exhaustDirection=new Vector3(0,.38f,-.925f);
        [SerializeField,Range(.2f,1.5f)] private float flameLength=.55f;
        [SerializeField,Range(.02f,.12f)] private float flameRadius=.05f;
        [Tooltip("Strength of the continuous nitro flame while Launch is active.")]
        [SerializeField,Range(.25f,2)] private float nitroFlameStrength=1;
        [SerializeField,Range(0,20)] private float exhaustEmberRate=5;
        [Header("Nitro ignition mini explosion")]
        [SerializeField] private ParticleSystem ignitionPrefab;
        [SerializeField,Range(.3f,1.5f)] private float ignitionScale=1;
        [Header("Nitro ignition camera impact")]
        [SerializeField,Range(0,.5f)] private float ignitionCameraKick=.18f;
        [SerializeField,Range(0,5)] private float ignitionCameraPitch=2;
        [Tooltip("Real seconds for the kick to rise and settle.")]
        [SerializeField,Range(.1f,.5f)] private float ignitionKickDuration=.22f;
        [SerializeField,Range(0,.1f)] private float ignitionShakePosition=.025f;
        [SerializeField,Range(0,3)] private float ignitionShakeRotation=.75f;
        [SerializeField,Range(.15f,.7f)] private float ignitionShakeDuration=.32f;
        [SerializeField,Range(6,25)] private float ignitionShakeFrequency=16;
        public float Amount { get; private set; }
        public float EntryPulse => Mathf.Exp(-entryAge*5);
        public float CameraFovOffset => fovIncrease*Amount;
        public float EffectClock => clock;
        public int EntryCount { get; private set; }
        public int ExhaustIgnitionCount { get; private set; }
        public int IgnitionEffectCount { get; private set; }
        public int LastIgnitionLayerCount { get; private set; }
        public int LastIgnitionParticleCount { get; private set; }
        public ParticleSystem IgnitionParticles => ignitionBurst;
        public float IgnitionCameraAge => ignitionCameraAge;
        public Vector3 LastIgnitionCameraOffset { get; private set; }
        public Vector3 LastIgnitionCameraRotation { get; private set; }
        public ParticleSystem WindParticles => windParticles;
        public int WindParticleCount => windParticles!=null ? windParticles.particleCount : 0;
        public int EmittedWindParticleCount { get; private set; }
        public ParticleSystem JacketWindParticles => jacketWindParticles;
        public int JacketWindParticleCount => jacketWindParticles!=null ? jacketWindParticles.particleCount : 0;
        public int EmittedJacketWindParticleCount { get; private set; }
        public Vector3 LastJacketWindOrigin { get; private set; }
        public Transform LastJacketWindAnchor { get; private set; }
        public int VisibleFlames {get {int n=0;foreach(var renderer in flameRenderers)if(renderer.enabled)n++;return n;}}
        public Vector3 ExhaustWorldPosition(int index)=>motorcycle.TransformPoint(exhaustPorts[index]);
        private BikeSwordCombat combat;
        private GameplayManager gameplay;
        private PlayerController player;
        private ParticleSystem windParticles;
        private ParticleSystemRenderer windRenderer;
        private readonly ParticleSystem.Particle[] windBuffer=new ParticleSystem.Particle[1024];
        private readonly System.Random windRandom=new System.Random(239410);
        private float windBudget;
        private MaterialPropertyBlock windProperties;
        private ParticleSystem jacketWindParticles;
        private ParticleSystemRenderer jacketWindRenderer;
        private MaterialPropertyBlock jacketWindProperties;
        private readonly System.Random jacketRandom=new System.Random(41879);
        private float jacketWindBudget;
        private float jacketUpperShare;
        private readonly List<MeshRenderer> flameRenderers=new List<MeshRenderer>();
        private readonly List<Mesh> flameMeshes=new List<Mesh>();
        private readonly List<ParticleSystem> embers=new List<ParticleSystem>();
        private readonly List<GameObject> owned=new List<GameObject>();
        private readonly System.Random random=new System.Random(87429);
        private readonly Vector3[] flameVertices=new Vector3[78];
        private readonly Color[] flameColors=new Color[78];
        private MaterialPropertyBlock flameProperties;
        private Camera windView;
        private Vector3 previousWindPosition;
        private LaunchScreen screen;
        private int lastLaunch;
        private float clock,entryAge=100,exhaustEmberBudget;
        private bool wasPaused;
        private ParticleSystem ignitionBurst;
        private float ignitionAge;
        private float ignitionCameraAge=100;
        private GameObject NewChild(string label,Transform parent) {
            var go=new GameObject(label);go.transform.SetParent(parent,false);owned.Add(go);return go;
        }
        private void Start() {
            combat=GetComponent<BikeSwordCombat>();player=GetComponent<PlayerController>();gameplay=FindAnyObjectByType<GameplayManager>();
            if(motorcycle==null)foreach(var filter in GetComponentsInChildren<MeshFilter>())if(filter.name=="Motorcycle"){motorcycle=filter.transform;break;}
            if(combat==null || motorcycle==null || flowMaterial==null || flameMaterial==null || windParticleTemplate==null){enabled=false;return;}
            flameProperties=new MaterialPropertyBlock();
            windProperties=new MaterialPropertyBlock();
            windView=Camera.main;previousWindPosition=transform.position;
            CreateWindParticles();
            CreateJacketWind();
            for(int i=0;i<exhaustPorts.Length;i++)CreateExhaust(i);
        }
        private void CreateExhaust(int index) {
            var go=NewChild("Launch exhaust "+index,motorcycle);go.transform.localPosition=exhaustPorts[index];go.transform.localRotation=Quaternion.LookRotation(exhaustDirection.normalized);
            var mesh=new Mesh{name="Launch flame "+index};mesh.MarkDynamic();
            var uv=new Vector2[78];var triangles=new int[216];int at=0;
            for(int card=0;card<3;card++) {
                for(int r=0;r<13;r++)for(int edge=0;edge<2;edge++)uv[card*26+r*2+edge]=new Vector2(r/12f,edge);
                for(int r=0;r<12;r++){int a=card*26+r*2;triangles[at++]=a;triangles[at++]=a+2;triangles[at++]=a+1;triangles[at++]=a+1;triangles[at++]=a+2;triangles[at++]=a+3;}
            }
            mesh.vertices=flameVertices;mesh.uv=uv;mesh.triangles=triangles;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=flameMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
            flameMeshes.Add(mesh);flameRenderers.Add(renderer);
            var sparks=NewChild("Launch exhaust embers "+index,go.transform).AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);sparks.useAutoRandomSeed=false;sparks.randomSeed=(uint)(87429+index);
            var main=sparks.main;main.loop=false;main.playOnAwake=false;main.maxParticles=48;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.22f);main.startSpeed=new ParticleSystem.MinMaxCurve(3,6);main.startSize=new ParticleSystem.MinMaxCurve(.012f,.027f);main.startColor=new Color(.3f,.7f,1,.8f);
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.025f;
            var color=sparks.colorOverLifetime;color.enabled=true;var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.45f,.1f),1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});color.color=fade;
            var draw=sparks.GetComponent<ParticleSystemRenderer>();draw.sharedMaterial=flowMaterial;draw.renderMode=ParticleSystemRenderMode.Stretch;draw.lengthScale=2;draw.velocityScale=.02f;draw.shadowCastingMode=ShadowCastingMode.Off;embers.Add(sparks);
        }
        private void Update() {
            if(combat==null || gameplay==null)return;
            bool paused=gameplay.gameState==GameState.Pause;
            if(paused!=wasPaused){
                foreach(var system in embers){if(paused)system.Pause();else if(system.isPaused)system.Play();}
                if(windParticles!=null){if(paused)windParticles.Pause();else if(windParticles.isPaused)windParticles.Play();}
                if(jacketWindParticles!=null){if(paused)jacketWindParticles.Pause();else if(jacketWindParticles.isPaused)jacketWindParticles.Play();}
                if(ignitionBurst!=null){if(paused)ignitionBurst.Pause(true);else ignitionBurst.Play(true);}
                wasPaused=paused;
            }
            if(paused)return;
            if(gameplay.gameState!=GameState.Playing || !player.IsAlive){Clear();return;}
            float realDt=Time.unscaledDeltaTime,dt=Time.deltaTime;clock+=dt;entryAge+=realDt;
            ignitionCameraAge+=realDt;
            if(ignitionBurst!=null){ignitionAge+=dt;if(ignitionAge>.75f){Destroy(ignitionBurst.gameObject);ignitionBurst=null;}}
            if(combat.IsPursuing && combat.LaunchCount!=lastLaunch) {
                lastLaunch=combat.LaunchCount;entryAge=0;EntryCount++;IgniteExhaust();
            }
            Amount=Mathf.MoveTowards(Amount,combat.IsPursuing ? 1 : 0,realDt/Mathf.Max(.03f,combat.IsPursuing ? entryTime : exitTime));
            GameAudio.Active?.SetLaunchMix(powerEngineLoop,Amount,engineLayerVolume,engineLayerPitch*Mathf.Lerp(.9f,1,Amount),engineGainDb,windGainDb);
            UpdateWindParticles(dt);UpdateFlames();
        }
        private void IgniteExhaust() {
            ExhaustIgnitionCount++;exhaustEmberBudget=0;
            ignitionCameraAge=0;
            if(ignitionPrefab!=null && exhaustPorts.Length>0) {
                if(ignitionBurst!=null)Destroy(ignitionBurst.gameObject);
                ignitionBurst=Instantiate(ignitionPrefab,motorcycle);ignitionBurst.name="Nitro ignition mini explosion";
                var centre=Vector3.zero;foreach(var port in exhaustPorts)centre+=port;centre/=exhaustPorts.Length;
                ignitionBurst.transform.localPosition=centre+exhaustDirection.normalized*.05f;
                ignitionBurst.transform.localRotation=Quaternion.LookRotation(exhaustDirection.normalized);ignitionBurst.transform.localScale=Vector3.one*ignitionScale;
                ignitionBurst.Simulate(1f/60f,true,true,false);ignitionBurst.Play(true);ignitionAge=0;
                IgnitionEffectCount++;var layers=ignitionBurst.GetComponentsInChildren<ParticleSystem>();LastIgnitionLayerCount=layers.Length;LastIgnitionParticleCount=0;
                foreach(var layer in layers)LastIgnitionParticleCount+=layer.particleCount;
            }
            if(flameRenderers.Count>0)GameAudio.Active?.PlayLaunchBurst(exhaustCrack,flameRenderers[0].transform,exhaustVolume*1.3f,.94f+(float)random.NextDouble()*.12f);
        }
        private void CreateWindParticles() {
            windParticles=Instantiate(windParticleTemplate,transform);windParticles.name="Launch wind particles";
            owned.Add(windParticles.gameObject);windParticles.transform.localPosition=Vector3.zero;windParticles.transform.localRotation=Quaternion.identity;windParticles.transform.localScale=Vector3.one;
            windParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);windParticles.useAutoRandomSeed=false;windParticles.randomSeed=239410;
            var main=windParticles.main;main.loop=true;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startSize3D=true;main.maxParticles=1024;main.simulationSpeed=1;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=windParticles.emission;emission.enabled=false;var shape=windParticles.shape;shape.enabled=false;
            var trails=windParticles.trails;trails.enabled=false;var velocity=windParticles.velocityOverLifetime;velocity.enabled=false;var force=windParticles.forceOverLifetime;force.enabled=false;var inherit=windParticles.inheritVelocity;inherit.enabled=false;var noise=windParticles.noise;noise.enabled=false;
            windRenderer=windParticles.GetComponent<ParticleSystemRenderer>();
            if(windParticleMaterial!=null)windRenderer.sharedMaterial=windParticleMaterial;
            var color=windParticles.colorOverLifetime;color.enabled=true;var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.65f),new GradientAlphaKey(0,1)});color.color=fade;
            windParticles.Play();
        }
        private float WindRange(float low,float high)=>low+(float)windRandom.NextDouble()*(high-low);
        private void UpdateWindParticles(float dt) {
            if(windParticles==null)return;
            windProperties.SetFloat("_Brightness",windParticleBrightness);windRenderer.SetPropertyBlock(windProperties);
            if(Amount<=.001f){windParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);windBudget=0;previousWindPosition=transform.position;return;}
            if(!windParticles.isPlaying)windParticles.Play();
            windBudget+=Mathf.Max(0,dt)*windEmissionRate*Amount*(combat.IsDashing ? .4f : 1);
            int emitCount=Mathf.Min(256,Mathf.FloorToInt(windBudget));windBudget-=emitCount;
            for(int i=0;i<emitCount;i++) {
                bool rear=windRandom.NextDouble()<windRearFraction;
                float angle=WindRange(0,Mathf.PI*2);
                var position=rear
                    ? windRearOffset+new Vector3(WindRange(-windRadius,windRadius),WindRange(-.18f,.18f),WindRange(-.25f,.25f))
                    : new Vector3(Mathf.Cos(angle)*windRadius,1.15f+Mathf.Sin(angle)*windRadius*.8f,WindRange(-.65f,.35f));
                float size=WindRange(.75f,1.25f);var tint=windParticleColor;tint.a*=Amount;
                var particle=new ParticleSystem.EmitParams {
                    position=position,velocity=new Vector3(WindRange(-.2f,.2f),WindRange(-.08f,.08f),-windParticleSpeed*WindRange(.85f,1.15f)),
                    startLifetime=windParticleLifetime*WindRange(.8f,1.2f),startSize3D=new Vector3(windParticleWidth*size,windParticleLength*size,windParticleWidth*size),
                    startColor=tint,randomSeed=(uint)(EmittedWindParticleCount+1)
                };
                windParticles.Emit(particle,1);EmittedWindParticleCount++;
            }
            // Keep isolated stretched sprites clear of the camera, even after
            // a shoulder/finisher camera move. No ribbon or particle trails.
            var cameraPosition=windView!=null ? windView.transform.position+(transform.position-previousWindPosition) : Vector3.zero;
            int count=windParticles.GetParticles(windBuffer);
            for(int i=0;i<count;i++)if(windView!=null && Vector3.Dot(windParticles.transform.TransformPoint(windBuffer[i].position)-cameraPosition,windView.transform.forward)<2.5f)windBuffer[i].remainingLifetime=0;
            windParticles.SetParticles(windBuffer,count);previousWindPosition=transform.position;
        }
        private void CreateJacketWind() {
            if(jacketWindAnchors==null || jacketWindAnchors.Length==0) {
                var anchors=new List<Transform>();
                foreach(var bone in GetComponentsInChildren<Transform>(true))if(bone.name=="jacket_01_03_end" || bone.name=="jacket_01_03.L_end" || bone.name=="jacket_01_03.R_end")anchors.Add(bone);
                jacketWindAnchors=anchors.ToArray();
            }
            if(jacketWindAnchors.Length==0)return;
            jacketWindParticles=Instantiate(windParticleTemplate,transform);jacketWindParticles.name="Launch jacket airflow";owned.Add(jacketWindParticles.gameObject);
            jacketWindParticles.transform.localPosition=Vector3.zero;jacketWindParticles.transform.localRotation=Quaternion.identity;jacketWindParticles.transform.localScale=Vector3.one;
            jacketWindParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);jacketWindParticles.useAutoRandomSeed=false;jacketWindParticles.randomSeed=41879;
            var main=jacketWindParticles.main;main.loop=true;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startSize3D=true;main.maxParticles=160;main.simulationSpeed=1;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=jacketWindParticles.emission;emission.enabled=false;var shape=jacketWindParticles.shape;shape.enabled=false;var trails=jacketWindParticles.trails;trails.enabled=false;
            var velocity=jacketWindParticles.velocityOverLifetime;velocity.enabled=false;var force=jacketWindParticles.forceOverLifetime;force.enabled=false;var inherit=jacketWindParticles.inheritVelocity;inherit.enabled=false;var noise=jacketWindParticles.noise;noise.enabled=false;
            var color=jacketWindParticles.colorOverLifetime;color.enabled=true;var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.6f,.65f),new GradientAlphaKey(0,1)});color.color=fade;
            jacketWindRenderer=jacketWindParticles.GetComponent<ParticleSystemRenderer>();if(windParticleMaterial!=null)jacketWindRenderer.sharedMaterial=windParticleMaterial;
            jacketWindProperties=new MaterialPropertyBlock();jacketWindParticles.Play();
        }
        private void LateUpdate() {
            // Read the finished animated pose, including the sword/jacket layer.
            if(jacketWindParticles==null || gameplay==null || gameplay.gameState!=GameState.Playing)return;
            jacketWindProperties.SetFloat("_Brightness",jacketWindBrightness);jacketWindRenderer.SetPropertyBlock(jacketWindProperties);
            float totalRate=Mathf.Max(0,jacketWindEmissionRate)+Mathf.Max(0,jacketWindUpperEmissionRate);
            if(Amount<=.001f || totalRate<=0){jacketWindParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);jacketWindBudget=jacketUpperShare=0;return;}
            if(!jacketWindParticles.isPlaying)jacketWindParticles.Play();
            jacketWindBudget+=Time.deltaTime*totalRate*Amount*(combat.IsDashing ? .4f : 1);
            int count=Mathf.Min(64,Mathf.FloorToInt(jacketWindBudget));jacketWindBudget-=count;
            for(int i=0;i<count;i++) {
                var anchor=jacketWindAnchors[EmittedJacketWindParticleCount%jacketWindAnchors.Length];if(anchor==null)continue;
                jacketUpperShare+=Mathf.Max(0,jacketWindUpperEmissionRate)/totalRate;
                bool above=jacketUpperShare>=1;if(above)jacketUpperShare-=1;
                var position=transform.InverseTransformPoint(anchor.position)+(above ? jacketWindUpperOffset : jacketWindOffset);
                LastJacketWindOrigin=transform.TransformPoint(position);LastJacketWindAnchor=anchor;
                float variation=.85f+(float)jacketRandom.NextDouble()*.3f;
                var tint=windParticleColor;tint.a*=jacketWindOpacity*Amount;
                var particle=new ParticleSystem.EmitParams {
                    position=position,velocity=new Vector3(((float)jacketRandom.NextDouble()-.5f)*.12f,-.06f,-jacketWindSpeed*variation),
                    startLifetime=jacketWindLifetime*variation,startSize3D=new Vector3(jacketWindWidth*variation,jacketWindLength*variation,jacketWindWidth*variation),startColor=tint,randomSeed=(uint)(EmittedJacketWindParticleCount+1)
                };
                jacketWindParticles.Emit(particle,1);EmittedJacketWindParticleCount++;
            }
        }
        private void UpdateFlames() {
            float envelope=Amount*nitroFlameStrength*(1+EntryPulse*.2f);
            exhaustEmberBudget+=Time.deltaTime*exhaustEmberRate*Amount;
            int sparks=Mathf.Min(16,Mathf.FloorToInt(exhaustEmberBudget));exhaustEmberBudget-=sparks;
            if(sparks>0)foreach(var system in embers){if(!system.isPlaying)system.Play();system.Emit(sparks);}
            for(int i=0;i<flameMeshes.Count;i++) {
                flameRenderers[i].enabled=envelope>.01f;if(!flameRenderers[i].enabled)continue;
                for(int card=0;card<3;card++)for(int r=0;r<13;r++)for(int edge=0;edge<2;edge++) {
                    float along=r/12f,theta=card*Mathf.PI/3;
                    float width=flameRadius*1.8f*Mathf.Lerp(.55f,1,Mathf.Sin(along*Mathf.PI))*(edge==0 ? -1 : 1);
                    float flicker=1+Mathf.Sin(clock*53+i*2+card*1.3f)*.13f;
                    var lateral=new Vector3(Mathf.Cos(theta),Mathf.Sin(theta),0)*width;
                    var curl=new Vector3(Mathf.Sin(along*7-clock*31+i),Mathf.Cos(along*5-clock*27+card),0)*along*.015f;
                    int v=card*26+r*2+edge;
                    flameVertices[v]=lateral+curl+Vector3.forward*(along*flameLength*envelope*flicker*(1-card*.12f));
                    flameColors[v]=new Color(1,1,1,Mathf.Clamp01(envelope*1.5f));
                }
                flameMeshes[i].vertices=flameVertices;flameMeshes[i].colors=flameColors;flameMeshes[i].RecalculateBounds();
                flameProperties.SetFloat("_FlameClock",clock+i*.17f);flameRenderers[i].SetPropertyBlock(flameProperties);
            }
        }
        public void ApplyCamera(Camera view) {
            if(view==null || gameplay==null)return;
            if(screen==null || screen.gameObject!=view.gameObject)screen=view.GetComponent<LaunchScreen>() ?? view.gameObject.AddComponent<LaunchScreen>();
            screen.Configure(borderShader,borderGlow*Amount,EntryPulse,borderWidth,borderColor);
            view.fieldOfView=Mathf.Clamp(view.fieldOfView+CameraFovOffset,15,120);
            view.transform.position+=view.transform.TransformDirection(cameraViewOffset)*Amount;
            view.transform.rotation*=Quaternion.Euler(cameraViewEuler*Amount);
            float attack=Mathf.Min(.035f,ignitionKickDuration*.3f);
            float kick=ignitionCameraAge<attack ? Mathf.SmoothStep(0,1,ignitionCameraAge/attack) : 1-Mathf.SmoothStep(0,1,(ignitionCameraAge-attack)/Mathf.Max(.01f,ignitionKickDuration-attack));
            float shakeTime=Mathf.Clamp01(ignitionCameraAge/Mathf.Max(.05f,ignitionShakeDuration));
            float envelope=Mathf.SmoothStep(0,1,ignitionCameraAge/.02f)*(1-shakeTime)*(1-shakeTime);
            float phase=ignitionCameraAge*ignitionShakeFrequency*Mathf.PI*2;
            float x=Mathf.Sin(phase),y=Mathf.Sin(phase*1.31f+.7f),z=Mathf.Sin(phase*.83f+1.4f);
            LastIgnitionCameraOffset=new Vector3(x*ignitionShakePosition*envelope,y*ignitionShakePosition*.65f*envelope+ignitionCameraKick*.15f*kick,-ignitionCameraKick*kick);
            LastIgnitionCameraRotation=new Vector3(-ignitionCameraPitch*kick+y*ignitionShakeRotation*envelope,x*ignitionShakeRotation*.4f*envelope,z*ignitionShakeRotation*.5f*envelope);
            // CameraController rebuilds its base pose each frame. Add this local
            // impulse to the existing steering/collision/sword camera channels.
            view.transform.position+=view.transform.TransformDirection(LastIgnitionCameraOffset);
            view.transform.rotation*=Quaternion.Euler(LastIgnitionCameraRotation);
        }
        private void Clear() {
            Amount=0;exhaustEmberBudget=0;entryAge=100;screen?.Clear();GameAudio.Active?.SetLaunchMix(powerEngineLoop,0,0,1,0,0);
            ignitionCameraAge=100;LastIgnitionCameraOffset=LastIgnitionCameraRotation=Vector3.zero;
            windBudget=0;
            if(windParticles!=null)windParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            jacketWindBudget=jacketUpperShare=0;
            if(jacketWindParticles!=null)jacketWindParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(ignitionBurst!=null){Destroy(ignitionBurst.gameObject);ignitionBurst=null;}
            foreach(var renderer in flameRenderers)renderer.enabled=false;foreach(var system in embers)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDisable(){Clear();}
        private void OnDestroy(){if(ignitionBurst!=null)Destroy(ignitionBurst.gameObject);if(screen!=null)Destroy(screen);foreach(var mesh in flameMeshes)if(mesh!=null)Destroy(mesh);foreach(var go in owned)if(go!=null)Destroy(go);}
    }
}
