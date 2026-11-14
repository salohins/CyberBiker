using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.Pooling;
using CyberBiker.Audio;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ShootingFeedbackChecks {
    private const string Key="CyberBiker.ShootingChecks";private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Neutral:IPlayerInput{public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    private sealed class AimingInput:IPlayerInput{public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>true;}
    private static PlayerController player;private static ShootingFeedback feedback;private static GameplayManager manager;private static Enemy target;private static GameObject bulletPrefab,wall;
    private static EnemyHitReaction reaction;private static int reactionBefore;private static Vector3 enemyPosition;
    private static int stage,shotBefore,hitBefore,missBefore,missSoundsBefore,hurtBefore,lastFrame=-1;private static float at,healthBefore,damageSetting,intervalSetting;private static double started,pauseAt;private static bool finishing;private static string report="";
    static ShootingFeedbackChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){Directory.CreateDirectory("Logs/ShootingFeedback");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Next(){stage++;at=Time.time;File.AppendAllText("Logs/ShootingFeedback/progress.txt",stage+"\n");}
    private static void Fire(GameObject owner,Vector3 origin,Vector3 direction,float damage=10){var go=PrefabPool.Rent(bulletPrefab,origin,Quaternion.LookRotation(direction));var bullet=go.GetComponent<Bullet>();bullet.Initialize(owner,damage);bullet.bulletSpeed=300;}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-started<100,"Shooting feedback timeout at stage "+stage);
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.4f)return;feedback=player.GetComponent<ShootingFeedback>();Require(feedback!=null,"Player prefab must contain feedback");
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.PistolInteractionMode=PistolInteraction.AimAndAutoFire;manager.SwitchGameState(GameState.Playing);damageSetting=manager.PlayerPistolDamage;intervalSetting=manager.PlayerPistolShotInterval;
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new Neutral());player.GetComponent<BikeAutoAim>().enabled=false;
            player.enabled=false;var body=player.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeAll;body.linearVelocity=Vector3.zero;
            Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;bulletPrefab=(GameObject)typeof(PlayerController).GetField("bullet",Private).GetValue(player);
            shotBefore=feedback.ShotCount;player.Shoot();player.Shoot();Require(feedback.ShotCount==shotBefore+1,"Camera recoil must follow a successful shot, with no extra recoil on cooldown");Next();return;
        }
        if(stage!=12 && lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(stage==1){Require(feedback.RecoilAmount>0,"Fired shot needs a camera impulse");var camera=Camera.main;feedback.RemoveCamera(camera);feedback.ApplyCamera(camera);var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            for(int i=0;i<100;i++){feedback.RemoveCamera(camera);feedback.ApplyCamera(camera);}Require(Vector3.Distance(position,camera.transform.position)<.0001f && Quaternion.Angle(rotation,camera.transform.rotation)<.01f && Mathf.Abs(fov-camera.fieldOfView)<.001f,"Camera offsets must not accumulate drift");Capture("Shot");Next();
        }else if(stage==2 && Time.time-at>.7f){
            Require(feedback.RecoilAmount<.01f,"Shot recoil must return promptly");target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(6,0,30),Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==3 && target.HasInitialized){target.enabled=false;reaction=target.GetComponent<EnemyHitReaction>();Require(reaction!=null && reaction.Torso!=null,"Enemy needs a configured rider hit reaction");reactionBefore=reaction.HitCount;enemyPosition=target.transform.position;CaptureEnemy("Before");healthBefore=target.CurrentHealth;hitBefore=feedback.ConfirmedHitCount;Fire(player.gameObject,target.transform.position+Vector3.back*6+Vector3.up*2,Vector3.forward,15);Next();
        }else if(stage==4 && feedback.ConfirmedHitCount>hitBefore && reaction.AppliedJoltDegrees>16){
            Require(target.CurrentHealth==healthBefore-15 && feedback.ConfirmedHitCount==hitBefore+1 && feedback.LiveMarkerCount==1,"One swept projectile should create one true damage marker");Require(reaction.HitCount==reactionBefore+1 && reaction.Sparks.particleCount>=40,"Actual damaging bullet must create one rider jolt and the denser contact spark burst");Require(target.transform.position==enemyPosition,"Body jolt must never move the enemy root");Require(reaction.AppliedJoltDegrees<=28,"Stronger body reaction must remain bounded");Require(reaction.AppliedKickDistance>.1f && reaction.AppliedKickDistance<.3f,"Body needs a visible, bounded upper-body dip");CaptureEnemy("Impact");Capture("ConfirmedHit");CheckSparkRendering();report+="PASS actual pistol projectile: 15 damage, exactly one confirmed marker, existing cooldown, short recoil, no camera drift.\n";
            target.SetEncounterRole(true,50);healthBefore=target.CurrentHealth;hitBefore=feedback.ConfirmedHitCount;Fire(player.gameObject,target.transform.position+Vector3.back*6+Vector3.up*2,Vector3.forward,15);Next();
        }else if(stage==5 && Time.time-at>.25f){
            Require(target.CurrentHealth==healthBefore && feedback.ConfirmedHitCount==hitBefore,"Queued enemy immunity must not create a false hit confirmation");Require(reaction.HitCount==reactionBefore+1,"Immune enemies must not play a damage jolt or sparks");target.SetEncounterRole(false,manager.EnemyDistanceFromPlayer);Fire(player.gameObject,target.transform.position+Vector3.back*6+Vector3.up*2,Vector3.forward,80);Next();
        }else if(stage==6 && feedback.ConfirmedHitCount>hitBefore){
            Require(target.IsFinisherReady && reaction.HitCount==reactionBefore+2,"Damaging stagger hit must retain finisher behavior and its own bounded jolt");Capture("StaggerHit");target.TakeDamage(new DamageInfo(1000,player.gameObject,target.transform.position+Vector3.up*2,Vector3.back,Vector3.forward));float floorHealth=target.CurrentHealth,remaining=target.FinisherTimeRemaining;int contacts=reaction.HitCount;target.TakeDamage(new DamageInfo(15,player.gameObject,target.transform.position+Vector3.up*2,Vector3.back,Vector3.forward));Require(target.CurrentHealth==floorHealth && target.FinisherTimeRemaining==remaining && reaction.HitCount==contacts+1,"Finisher-floor bullet contacts must still react without health loss or timer refresh");report+="PASS physical bullet contacts at the finisher floor still react without changing health or the expiry timer.\n";report+="PASS queued immunity suppresses markers; damaging stagger hit retains finisher behavior and amber marker.\n";
            healthBefore=player.Health.Current;missBefore=feedback.NearMissCount;missSoundsBefore=GameAudio.Active.BulletNearMissSoundCount;hurtBefore=feedback.DamageCount;Fire(target.gameObject,feedback.NearMissCenter+Vector3.forward*7,Vector3.back,10);Next();
        }else if(stage==7 && feedback.DamageCount>hurtBefore){
            Require(player.Health.Current==healthBefore-10 && feedback.NearMissCount==missBefore,"Actual incoming hit must damage and never report a near miss");Capture("PlayerDamage");Require(Camera.main.GetComponent<CombatDamageScreen>().RenderCount>0,"Damage shader must actually render");report+="PASS real incoming projectile triggers damage, directional screen effect and jolt, with no false near miss.\n";
            healthBefore=player.Health.Current;Fire(target.gameObject,feedback.NearMissCenter+Vector3.left*1.65f+Vector3.back*8,Vector3.forward,10);Next();
        }else if(stage==8 && feedback.NearMissCount>missBefore){
            Require(player.Health.Current==healthBefore && feedback.NearMissCount==missBefore+1,"Close left pass must give feedback without dealing damage");Capture("NearMissLeft");Fire(target.gameObject,feedback.NearMissCenter+Vector3.right*2.85f+Vector3.back*8,Vector3.forward,10);Fire(target.gameObject,feedback.NearMissCenter+Vector3.left*2.85f+Vector3.back*8,Vector3.forward,10);Next();
        }else if(stage==9 && Time.time-at>.25f){
            Require(feedback.NearMissCount==missBefore+3 && GameAudio.Active.BulletNearMissSoundCount==missSoundsBefore+3,"Every distinct bullet in a close burst must play once, including the wider 2.85-unit passes; no shared cooldown or repeats");Fire(target.gameObject,feedback.NearMissCenter+Vector3.right*1.65f+Vector3.back*8,Vector3.forward,10);Next();
        }else if(stage==10 && feedback.NearMissCount==missBefore+4){
            Require(player.Health.Current==healthBefore,"Right-side whiz must not cause damage");Capture("NearMissRight");wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=feedback.NearMissCenter+Vector3.left*1.65f+Vector3.back*4;wall.transform.localScale=new Vector3(1,3,.3f);wall.layer=LayerMask.NameToLayer("WorldObject");Physics.SyncTransforms();
            Fire(target.gameObject,feedback.NearMissCenter+Vector3.left*1.65f+Vector3.back*8,Vector3.forward,10);Next();
        }else if(stage==11 && Time.time-at>.3f){
            Require(feedback.NearMissCount==missBefore+4,"A shot stopped before the player must not create a through-wall whiz");UnityEngine.Object.DestroyImmediate(wall);Fire(player.gameObject,feedback.NearMissCenter+Vector3.left*1.65f+Vector3.back*8,Vector3.forward,10);Fire(target.gameObject,feedback.NearMissCenter+Vector3.right*6f+Vector3.back*8,Vector3.forward,10);
            report+="PASS left/right close passes at 1.65 and 2.85 units, every bullet in a burst audible without a shared cooldown, exactly one cue per projectile, pooled reset and blocked-shot suppression.\n";Next();
        }else if(stage==12 && Time.time-at>.3f){
            Require(feedback.NearMissCount==missBefore+4,"Friendly shots and distant misses must be silent");feedback.ConfirmHit(target.transform,target.transform.position+Vector3.up*2,Vector3.back,false);hitBefore=feedback.LiveMarkerCount;manager.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;Next();
        }else if(stage==13 && EditorApplication.timeSinceStartup-pauseAt>.2){
            Require(feedback.LiveMarkerCount==hitBefore,"Pause must freeze marker timers");int shots=feedback.ShotCount;player.Shoot();Require(feedback.ShotCount==shots,"Pause cannot fire or add recoil");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==14 && Time.time-at>.7f){
            Require(reaction.AppliedJoltDegrees==0 && reaction.AppliedKickDistance==0 && reaction.Sparks.particleCount==0,"Enemy reaction and spark burst must finish cleanly");CaptureEnemy("Recovered");report+="PASS enemy rider jolts and contact sparks on each damaging bullet, no immune reactions, unchanged enemy root, clean recovery and finisher readiness.\n";Require(feedback.LiveMarkerCount==0 && feedback.DamageAmount==0 && feedback.RecoilAmount<.001f,"Feedback must finish and cleanly return to normal");Require(manager.PlayerPistolDamage==damageSetting && manager.PlayerPistolShotInterval==intervalSetting,"Presentation must not change pistol balance");
            Require(GameAudio.Active.BulletNearMissSoundCount==missSoundsBefore+4 && GameAudio.Active.PistolHitSoundCount>=3,"Assigned real audio cues must play");var hitClip=(AudioClip)typeof(GameAudio).GetField("pistolHitConfirm",Private).GetValue(GameAudio.Active);var missClip=(AudioClip)typeof(GameAudio).GetField("bulletNearMiss",Private).GetValue(GameAudio.Active);
            Require(hitClip.name=="PistolHit" && hitClip.length<.25f && missClip.name=="BulletWhiz" && missClip.length<.3f,"New short recorded impact and natural air-swish cues must be assigned");foreach(var cue in new[]{hitClip,missClip}){var imported=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(cue));Require(imported.defaultSampleSettings.preloadAudioData && imported.defaultSampleSettings.compressionFormat==AudioCompressionFormat.PCM,"Short combat cues must preload without lossy transient smearing");}Capture("Recovered");
            report+="PASS friendly/distant rejection, pause, recovery, assigned audio, unchanged tuning and pooled lifecycle.\n";
            target.gameObject.SetActive(false);target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(6,0,30),Quaternion.identity).GetComponent<Enemy>();
            target.enabled=false;typeof(Enemy).GetField("vitality",Private).SetValue(target,new Health(1000));
            typeof(PlayerController).GetField("input",Private).SetValue(player,new AimingInput());player.GetComponent<BikeAutoAim>().enabled=true;player.GetComponent<TouchInputManager>().SetAim(true);player.enabled=true;hitBefore=feedback.ConfirmedHitCount;shotBefore=feedback.ShotCount;Next();
        }else if(stage==15 && feedback.ConfirmedHitCount>hitBefore){
            Require(player.GetComponent<BikeAutoAim>().Target==target && feedback.ShotCount>shotBefore && target.CurrentHealth<1000,"Normal aim/draw/automatic pistol flow must still hit with feedback enabled");
            CheckMarkerAlignment();Capture("AutomaticAimHit");report+="PASS actual aim/draw/automatic shooting, overlay hit-marker alignment and stable camera integration.\n";
            player.GetComponent<TouchInputManager>().SetAim(false);typeof(PlayerController).GetField("input",Private).SetValue(player,new Neutral());Next();
        }else if(stage==16 && Time.time-at>1.2f){
            var screen=Camera.main.GetComponent<CombatDamageScreen>();Require(!screen.enabled,"Idle feedback must not run an extra fullscreen render pass");
            var particles=(ParticleSystem)typeof(ShootingFeedback).GetField("slipstream",Private).GetValue(feedback);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            feedback.CheckNearMiss(feedback.NearMissCenter+Vector3.left*1.65f+Vector3.back*3,feedback.NearMissCenter+Vector3.left*1.65f+Vector3.forward*3,.02f);
            Require(particles.isPlaying && particles.particleCount>0,"Near-miss particles must restart after their system has gone idle");
            Finish(true,report+"PASS idle screen bypass and near-miss emitter restart.\n");
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void CheckSparkRendering(){
        var renderer=reaction.Sparks.GetComponent<ParticleSystemRenderer>();renderer.enabled=false;
        try{Capture("NoContactSparks");}finally{renderer.enabled=true;}
        var visible=new Texture2D(2,2);var absent=new Texture2D(2,2);
        try{
            visible.LoadImage(File.ReadAllBytes("Logs/ShootingFeedback/ConfirmedHit.png"));absent.LoadImage(File.ReadAllBytes("Logs/ShootingFeedback/NoContactSparks.png"));
            var a=visible.GetPixels32();var b=absent.GetPixels32();int changed=0;
            for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>35)changed++;
            Require(changed>=80,"Contact sparks must be visibly readable from the actual gameplay camera, changed pixels="+changed);
            report+="PASS contact sparks render "+changed+" distinct pixels from the normal gameplay camera.\n";
        }finally{UnityEngine.Object.DestroyImmediate(visible);UnityEngine.Object.DestroyImmediate(absent);}
    }
    private static void CaptureEnemy(string name){
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try{camera.transform.position=target.transform.position+new Vector3(-5,4,-7);camera.transform.LookAt(target.transform.position+Vector3.up*2.3f);camera.fieldOfView=45;Capture("Enemy"+name);}
        finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    private static void CheckMarkerAlignment(){
        var camera=Camera.main;Canvas.ForceUpdateCanvases();typeof(ShootingFeedback).GetMethod("DrawMarkers",Private).Invoke(feedback,new object[]{camera});
        var markers=(Array)typeof(ShootingFeedback).GetField("markers",Private).GetValue(feedback);bool checkedOne=false;
        foreach(var marker in markers){var type=marker.GetType();if((float)type.GetField("time").GetValue(marker)<=0)continue;
            var rt=(RectTransform)type.GetField("root").GetValue(marker);var world=(Vector3)type.GetField("worldPoint").GetValue(marker);
            var expected=(Vector2)camera.WorldToScreenPoint(world);var actual=RectTransformUtility.WorldToScreenPoint(null,rt.position);
            Require(Vector2.Distance(expected,actual)<2,"Hit marker must line up with the real impact in the overlay canvas; expected="+expected+" actual="+actual);checkedOne=true;
        }
        Require(checkedOne,"Need a visible live marker for alignment verification");
    }
    private static void Capture(string name){var camera=Camera.main;var canvas=GameObject.Find("Combat feedback HUD").GetComponent<Canvas>();var mode=canvas.renderMode;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1280f/720;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.05f;Canvas.ForceUpdateCanvases();typeof(ShootingFeedback).GetMethod("DrawMarkers",Private).Invoke(feedback,new object[]{camera});camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/ShootingFeedback/"+name+".png",image.EncodeToPNG());
            if(name=="AutomaticAimHit"){
                var hidden=new Texture2D(1280,720,TextureFormat.RGB24,false);canvas.enabled=false;camera.Render();RenderTexture.active=rt;hidden.ReadPixels(new Rect(0,0,1280,720),0,0);hidden.Apply();canvas.enabled=true;
                var visible=image.GetPixels32();var absent=hidden.GetPixels32();int changed=0;for(int i=0;i<visible.Length;i++)if(Mathf.Abs(visible[i].r-absent[i].r)+Mathf.Abs(visible[i].g-absent[i].g)+Mathf.Abs(visible[i].b-absent[i].b)>35)changed++;
                UnityEngine.Object.DestroyImmediate(hidden);Require(changed>30,"Hit marker must actually render distinct pixels over the world; changed="+changed);report+="PASS marker rendering: "+changed+" changed pixels against actual gameplay.\n";
            }}
        finally{canvas.renderMode=mode;camera.targetTexture=previous;camera.ResetAspect();RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);Canvas.ForceUpdateCanvases();}}
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/ShootingFeedback/Result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
