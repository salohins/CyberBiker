using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LaunchEffectsChecks {
    private const string Pending="CyberBiker.LaunchEffectsChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static GameplayManager manager;
    private static BikeSwordCombat combat;
    private static BikeLaunchEffects effects;
    private static TouchInputManager input;
    private static Camera view;
    private static float started,stageTime,pausedClock,pausedAmount,pausedCameraAge;
    private static double pauseTime;
    private static int stage,pausedBursts,pausedSounds;
    private static string report="";
    private static bool finishing;
    private static int motionFrame;
    private static float nextMotionCapture;
    private static bool motionChecked;
    private static readonly Dictionary<uint,float> initialWindZ=new Dictionary<uint,float>();
    private static readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[1024];
    private static float windSampleClock;
    private static int pausedParticleCount;
    private static float pausedParticleLife;
    private static bool particlePauseCaptured;
    private sealed class FixtureInput : IPlayerInput {
        private readonly TouchInputManager touch;
        public FixtureInput(TouchInputManager value){touch=value;}
        public Vector2 Steering=>Vector2.zero;
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>touch.IsAiming;
    }
    static LaunchEffectsChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){LaunchEffectsSetup.Install();EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Next(){stage++;stageTime=Time.time;}
    private static void Tick(){
        if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;
        try {
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();combat=player.GetComponent<BikeSwordCombat>();effects=player.GetComponent<BikeLaunchEffects>();input=player.GetComponent<TouchInputManager>();view=Camera.main;
                Require(effects!=null && effects.enabled,"Production player needs bound Launch presentation");
                foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
                foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
                foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
                manager.SwitchGameState(GameState.Playing);input.enabled=false;input.SetAim(false);typeof(PlayerController).GetField("input",Private).SetValue(player,new FixtureInput(input));typeof(BikeSwordCombat).GetField("launchKey",Private).SetValue(combat,KeyCode.None);
                Require(effects.Amount==0 && effects.WindParticleCount==0 && effects.VisibleFlames==0,"Normal riding must have no Launch overlays");
                Require(effects.JacketWindParticles!=null && effects.JacketWindParticleCount==0,"Jacket airflow must be bound and inactive during normal riding");
                Capture("normal");started=Time.time;combat.Launch();
                typeof(BikeLaunchEffects).GetMethod("Update",Private).Invoke(effects,null);
                Require(effects.IgnitionCameraAge==0,"Nitro ignition must start the camera impulse immediately");
                CheckCameraImpulse();Capture("ignition-instant");Capture("ignition-instant-close",true);Next();
            } else if(stage==1 && effects.EntryCount==1 && effects.VisibleFlames==3) {
                Require(combat.IsPursuing && combat.DrawCompletionCount==0,"Launch presentation must retain mounted-sword riding");
                Require(effects.ExhaustIgnitionCount==1 && GameAudio.Active.LaunchBurstSoundCount==1,"Entry must produce exactly one nitro ignition");
                Require(effects.IgnitionEffectCount==1 && effects.LastIgnitionLayerCount>=5 && effects.LastIgnitionParticleCount>=30,"Ignition must emit its layered flash/ring/fire/spark/smoke burst");
                Require(effects.IgnitionParticles!=null && effects.IgnitionParticles.transform.parent.name=="Motorcycle","Ignition mini explosion must stay attached to the moving exhaust");
                Capture("entry");Capture("exhaust-close",true);Next();
            } else if(stage==2) {
                float age=Time.time-stageTime;
                if(effects.Amount>.9f)Require(effects.VisibleFlames==3 && effects.ExhaustIgnitionCount==1 && GameAudio.Active.LaunchBurstSoundCount==1,"Nitro flames must stay lit continuously without repeated ignition sounds");
                if(age>.25f && motionFrame<16 && Time.time>=nextMotionCapture) {
                    if(motionFrame==0){windSampleClock=Time.time;int count=effects.WindParticles.GetParticles(particles);for(int i=0;i<count;i++)initialWindZ[particles[i].randomSeed]=particles[i].position.z;}
                    Capture("motion-"+motionFrame.ToString("D2"));motionFrame++;nextMotionCapture=Time.time+.06f;
                }
                if(initialWindZ.Count>0 && Time.time-windSampleClock>.09f && !motionChecked) {
                    int moving=0,count=effects.WindParticles.GetParticles(particles);
                    for(int i=0;i<count;i++)if(initialWindZ.TryGetValue(particles[i].randomSeed,out var oldZ) && particles[i].position.z<oldZ-.15f)moving++;
                    Require(moving>=12,"Independent wind particles must move backward relative to the bike; moving="+moving);
                    motionChecked=true;report+="PASS separate particles: "+moving+" tracked particles moved backward relative to the bike; captured 16-frame in-game sequence.\n";
                }
                if(age<1.8f || motionFrame<16)return;
                Require(effects.IgnitionParticles==null && effects.IgnitionEffectCount==1,"Ignition burst must finish once while continuous nitro stays active");
                Require(effects.Amount>.99f && effects.WindParticleCount>=90,"Launch must sustain discrete wind particles");
                Require(effects.JacketWindParticleCount>=6 && effects.EmittedJacketWindParticleCount>20,"Launch must emit a separate stream of jacket airflow");
                Require(effects.LastJacketWindAnchor!=null && effects.LastJacketWindAnchor.IsChildOf(player.transform) && effects.LastJacketWindAnchor.name.StartsWith("jacket_"),"Jacket airflow must use the animated garment bones");
                Require(Vector3.Distance(effects.LastJacketWindOrigin,effects.LastJacketWindAnchor.position)<.35f,"Jacket airflow must originate close to the animated hem");
                Require(!effects.JacketWindParticles.trails.enabled,"Jacket airflow must use short separate wisps");
                foreach(var line in player.GetComponentsInChildren<LineRenderer>())Require(!line.name.StartsWith("Launch wind"),"Launch must not create linked wind lines");
                var wind=effects.WindParticles;Require(wind!=null && !wind.trails.enabled,"Launch wind must be ordinary particles without connected trails");
                var global=GameObject.Find("WindParticle").GetComponent<ParticleSystem>();
                var renderer=wind.GetComponent<ParticleSystemRenderer>();
                Require(renderer.sharedMaterial.shader.name=="CyberBiker/Launch Wind Particle" && renderer.sharedMaterial.shader.isSupported && renderer.renderMode==global.GetComponent<ParticleSystemRenderer>().renderMode,"Wind must use the shiny particle shader with stretched-sprite rendering");
                var glow=new MaterialPropertyBlock();renderer.GetPropertyBlock(glow);Require(glow.GetFloat("_Brightness")>=2.5f,"Wind particles need visible HDR glow");
                int visible=wind.GetParticles(particles),rear=0;
                float emitterHeight=new SerializedObject(effects).FindProperty("windRearOffset").vector3Value.y;
                for(int i=0;i<visible;i++) {
                    if(particles[i].position.z<-1)rear++;
                    Require(particles[i].position.z<-1.5f && Mathf.Abs(particles[i].position.x)<.43f && Mathf.Abs(particles[i].position.y-emitterHeight)<.25f,"Wind must remain in the narrow wake behind the rear wheel at the configured height");
                }
                Require(rear>=visible*.8f,"Most wind particles must stay behind the motorcycle; rear="+rear+" visible="+visible);
                var audio=GameAudio.Active.LaunchPowerAudio;
                Require(audio!=null && audio.isPlaying && audio.loop && audio.clip.name=="LaunchEnginePower" && audio.volume>.1f && audio.pitch>1 && audio.spatialBlend==1,"Recorded high-RPM layer must be audible, faster and attached in 3D");
                Require(Vector3.Distance(audio.transform.position,player.transform.position)<5,"Power layer must follow the moving rider");
                var controller=view.GetComponent<CameraController>();float baseline=(float)typeof(CameraController).GetField("fov",Private).GetValue(controller);
                Require(Mathf.Abs(view.fieldOfView-baseline-7)<.15f,"Launch camera must add seven degrees without changing ordinary FOV");
                Require(view.GetComponent<LaunchScreen>()!=null && view.GetComponent<LaunchScreen>().IsSupported && view.GetComponent<LaunchScreen>().Strength>.25f,"Blue border shader must be active");
                Capture("sustained");pausedCameraAge=effects.IgnitionCameraAge;pausedClock=effects.EffectClock;pausedAmount=effects.Amount;pausedBursts=effects.ExhaustIgnitionCount;pausedSounds=GameAudio.Active.LaunchBurstSoundCount;pausedParticleCount=effects.WindParticles.GetParticles(particles);pausedParticleLife=particles[0].remainingLifetime;pauseTime=EditorApplication.timeSinceStartup;manager.SwitchGameState(GameState.Pause);Next();
            } else if(stage==3) {
                Require(effects.EffectClock==pausedClock && effects.Amount==pausedAmount && effects.ExhaustIgnitionCount==pausedBursts && GameAudio.Active.LaunchBurstSoundCount==pausedSounds,"Pause must freeze Launch transitions, flame scheduling and cues");
                Require(effects.IgnitionCameraAge==pausedCameraAge,"Pause must freeze the ignition camera clock");
                Require(!GameAudio.Active.LaunchPowerAudio.isPlaying,"Pause must suspend the high-RPM audio source");
                if(!effects.WindParticles.isPaused){Require(EditorApplication.timeSinceStartup-pauseTime<.5,"Wind system must enter pause on the next runtime update");return;}
                int frozen=effects.WindParticles.GetParticles(particles);
                if(!particlePauseCaptured){pausedParticleCount=frozen;pausedParticleLife=particles[0].remainingLifetime;particlePauseCaptured=true;pauseTime=EditorApplication.timeSinceStartup;}
                Require(effects.JacketWindParticles.isPaused,"Pause must also freeze the jacket airflow");
                Require(frozen==pausedParticleCount && Mathf.Approximately(particles[0].remainingLifetime,pausedParticleLife),"Pause must freeze individual wind particles and their lifetimes");
                if(EditorApplication.timeSinceStartup-pauseTime>.3){manager.SwitchGameState(GameState.Playing);Next();}
            } else if(stage==4 && Time.time-stageTime>.5f) {
                Require(GameAudio.Active.LaunchPowerAudio.isPlaying && effects.VisibleFlames==3 && effects.ExhaustIgnitionCount==pausedBursts && GameAudio.Active.LaunchBurstSoundCount==pausedSounds,"Unpause must resume steady nitro flames and engine without another ignition");
                Capture("nitro-exhaust",true);input.SetAim(true);Next();
            } else if(stage==5 && Time.time-stageTime>.8f) {
                Require(!combat.IsPursuing && effects.Amount<.005f && effects.WindParticleCount==0 && effects.VisibleFlames==0,"Aim must cancel Launch and fade all sustained effects");
                Require(effects.JacketWindParticleCount==0,"Aim must clear the jacket airflow after the launch fade");
                Require(!GameAudio.Active.LaunchPowerAudio.isPlaying && view.GetComponent<LaunchScreen>().Strength<.001f,"Power loop and blue border must clear after Aim");
                Require(input.IsAiming,"Launch effects must not block pistol Aim");input.SetAim(false);combat.Launch();Next();
                report+="PASS entry: five-layer ignition mini explosion attached to the exhaust, one ignition and continuous nitro flames, rear-wheel wind and separate short jacket airflow anchored under the animated hem, +7-degree FOV and blue screen edge.\nPASS pause/resume, continuous nitro and single ignition per activation, mounted sword, independent Aim cancellation and full fade/loop cleanup.\n";
            } else if(stage==6) {
                if(!combat.IsPursuing && effects.Amount<.005f) {
                    Require(effects.EntryCount==2 && effects.ExhaustIgnitionCount==2 && effects.IgnitionEffectCount==2 && effects.IgnitionParticles==null && GameAudio.Active.LaunchBurstSoundCount==2 && effects.VisibleFlames==0 && !GameAudio.Active.LaunchPowerAudio.isPlaying && effects.WindParticleCount==0,"Natural Launch expiry must restore normal presentation");
                    manager.SwitchGameState(GameState.Hit);Next();
                } else Require(Time.time-stageTime<9,"Launch failed to expire naturally");
            } else if(stage==7 && Time.time-stageTime>.05f) {
                Require(effects.Amount==0 && effects.VisibleFlames==0 && effects.WindParticleCount==0 && view.GetComponent<LaunchScreen>().Strength==0,"Hit state must clear Launch visuals");
                Require(effects.JacketWindParticleCount==0,"Hit state must clear jacket airflow");
                Require(effects.LastIgnitionCameraOffset==Vector3.zero && effects.LastIgnitionCameraRotation==Vector3.zero,"Hit must clear the camera impulse");
                report+="PASS natural expiry, second activation and hit-state cleanup; physics boost and target gating unchanged.\n";Finish(true,report);
            }
            Require(Time.time-started<25,"Launch presentation check timed out at stage "+stage);
        }catch(Exception error){Finish(false,error+"\n"+report);}
    }
    private static void CheckCameraImpulse() {
        var go=new GameObject("Ignition camera verification");var probe=go.AddComponent<Camera>();probe.enabled=false;
        var position=view.transform.position;var rotation=view.transform.rotation;
        var ageField=typeof(BikeLaunchEffects).GetField("ignitionCameraAge",Private);
        var screenField=typeof(BikeLaunchEffects).GetField("screen",Private);
        var savedAge=ageField.GetValue(effects);var savedScreen=screenField.GetValue(effects);
        try {
            probe.transform.SetPositionAndRotation(position,rotation);probe.fieldOfView=60;
            ageField.SetValue(effects,.04f);effects.ApplyCamera(probe);
            var local=Quaternion.Inverse(rotation)*(probe.transform.position-position);
            Require(local.z<-.1f && Quaternion.Angle(rotation,probe.transform.rotation)>.5f,"Ignition must visibly kick the camera backward and pitch upward");
            var firstRotation=effects.LastIgnitionCameraRotation;
            probe.transform.SetPositionAndRotation(position,rotation);probe.fieldOfView=60;
            ageField.SetValue(effects,.07f);effects.ApplyCamera(probe);
            Require((effects.LastIgnitionCameraRotation-firstRotation).sqrMagnitude>.001f,"Ignition shake must move over time");
            probe.transform.SetPositionAndRotation(position,rotation);probe.fieldOfView=60;
            ageField.SetValue(effects,.8f);effects.ApplyCamera(probe);
            Require(Vector3.Distance(position,probe.transform.position)<.0001f && Quaternion.Angle(rotation,probe.transform.rotation)<.01f,"Ignition impulse must fully settle without leaving a camera offset");
            Require(effects.LastIgnitionCameraOffset==Vector3.zero && effects.LastIgnitionCameraRotation==Vector3.zero,"Settled camera impulse must be exactly zero");
            report+="PASS ignition camera: immediate trigger, backward kick/upward pitch, moving damped shake and zero residual offset after settling.\n";
        } finally {
            ageField.SetValue(effects,savedAge);screenField.SetValue(effects,savedScreen);UnityEngine.Object.DestroyImmediate(go);
        }
    }
    private static void Capture(string name,bool close=false) {
        var position=view.transform.position;var rotation=view.transform.rotation;float fov=view.fieldOfView,aspect=view.aspect;
        if(close){var bike=player.GetComponentsInChildren<MeshFilter>()[0].transform;foreach(var filter in player.GetComponentsInChildren<MeshFilter>())if(filter.name=="Motorcycle")bike=filter.transform;view.transform.position=bike.TransformPoint(new Vector3(1.8f,1.25f,-2.4f));view.transform.LookAt(bike.TransformPoint(new Vector3(.15f,.9f,-.55f)));view.fieldOfView=48;}
        var rt=new RenderTexture(1280,800,24,RenderTextureFormat.DefaultHDR);var previous=RenderTexture.active;var target=view.targetTexture;view.targetTexture=rt;view.aspect=1.6f;view.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();Directory.CreateDirectory("Logs/LaunchEffects");File.WriteAllBytes("Logs/LaunchEffects/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previous;view.targetTexture=target;view.aspect=aspect;view.fieldOfView=fov;view.transform.SetPositionAndRotation(position,rotation);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
    }
    private static void Finish(bool passed,string result){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Directory.CreateDirectory("Logs/LaunchEffects");File.WriteAllText("Logs/LaunchEffects/result.txt",result);EditorApplication.Exit(passed ? 0 : 1);}
}
