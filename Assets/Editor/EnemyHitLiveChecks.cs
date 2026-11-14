using System;
using System.IO;
using System.Reflection;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyHitLiveChecks {
    private const string Key="CyberBiker.EnemyHitLive";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class AimInput:IPlayerInput {public bool active=true;public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>active;}
    private static AimInput input;
    private static PlayerController player;private static Enemy enemy;private static EnemyHitReaction reaction;
    private static ShootingFeedback feedback;private static BikeAutoAim aim;private static GameplayManager manager;
    private static int stage,lastFrame=-1,seen,shotStart,confirmedStart,floorReaction;
    private static float startTime,hitAt,floorHealth,floorTime;
    private static CameraController frozenCamera;private static Animator frozenAnimator;private static bool cameraEnabled,frozen;private static float animatorSpeed;private static int frozenFrame;
    private static double began;private static bool finishing,normalCaptured,readyCaptured;
    private static string report="";private static bool trafficSeen;
    static EnemyHitLiveChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyHitLive");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-began<90,"Live hit check timed out: "+report);
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.PistolInteractionMode=PistolInteraction.AimAndAutoFire;manager.SwitchGameState(GameState.Playing);
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var old in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
            // Preserve live traffic, navigation, enemy animation and aim camera.
            // Only prevent the unattended test rider from crashing or dying.
            foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;
            player.GetComponent<TouchInputManager>().enabled=false;input=new AimInput();typeof(PlayerController).GetField("input",Private).SetValue(player,input);
            var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
            player.ConfigureTestingMovement(70,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);body.linearVelocity=Vector3.forward*70;
            manager.PlayerPistolDamage=15;manager.PlayerPistolShotInterval=.45f;manager.EnemyDistanceFromPlayer=20;
            Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
            var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();var advance=Vector3.forward*Mathf.Max(0,world.carSpawnStartDistance-65-player.transform.position.z);player.transform.position+=advance;body.position=player.transform.position;Camera.main.transform.position+=advance;var prefab=(GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world);
            enemy=UnityEngine.Object.Instantiate(prefab,player.transform.position+new Vector3(2,0,20),Quaternion.identity).GetComponent<Enemy>();
            var difficulty=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();enemy.SetRidingLane(.65f,difficulty.currentDifficulty);
            reaction=enemy.GetComponent<EnemyHitReaction>();feedback=player.GetComponent<ShootingFeedback>();aim=player.GetComponent<BikeAutoAim>();
            player.GetComponent<TouchInputManager>().SetAim(true);shotStart=feedback.ShotCount;confirmedStart=feedback.ConfirmedHitCount;startTime=Time.time;stage=1;return;
        }
        Require(Time.time-startTime<25,"Live aiming did not complete; shots="+(feedback.ShotCount-shotStart)+" reactions="+reaction.HitCount+" health="+enemy.CurrentHealth+" target="+(aim.Target!=null ? aim.Target.name : "none"));
        foreach(var car in Car.Instances)if(car!=null && car.gameObject.activeInHierarchy && Vector3.Distance(car.transform.position,player.transform.position)<100)trafficSeen=true;
        if(reaction.HitCount!=seen){seen=reaction.HitCount;hitAt=Time.time;File.AppendAllText("Logs/EnemyHitLive/Hits.txt","hit="+seen+" health="+enemy.CurrentHealth+" distance="+Vector3.Distance(player.transform.position,enemy.transform.position)+" speed="+enemy.DrivingVelocity+" contact="+reaction.LastContactPoint+"\n");}
        if(stage==1 && seen>0 && Time.time-hitAt>.04f && reaction.Sparks.particleCount>0){
            Require(aim.Target==enemy && enemy.enabled && enemy.DrivingVelocity.magnitude>20,"Use a real moving target and automatic aim");
            if(!enemy.IsFinisherReady && !normalCaptured){Capture("MovingAimHit");normalCaptured=true;report+="PASS actual automatic-aim hit with live traffic, enabled navigation/animation and 70 m/s riding.\n";}
            if(enemy.IsFinisherReady && !readyCaptured){
                Capture("FinisherReadyHit");readyCaptured=true;floorReaction=reaction.HitCount;floorHealth=enemy.CurrentHealth;floorTime=enemy.FinisherTimeRemaining;stage=2;
            }
        }else if(stage==2 && enemy.CurrentHealth<=1 && Time.time-hitAt>.04f){
            floorReaction=reaction.HitCount;floorHealth=enemy.CurrentHealth;floorTime=enemy.FinisherTimeRemaining;stage=3;
        }else if(stage==3 && reaction.HitCount>floorReaction && Time.time-hitAt>.04f && reaction.Sparks.particleCount>0){
            Require(enemy.IsFinisherReady && enemy.CurrentHealth==floorHealth && enemy.FinisherTimeRemaining<floorTime,"Floor contacts must keep the original health floor and expiry countdown");
            Capture("FinisherFloorHit");report+="PASS continued real pistol contacts still jolt and spark at the finisher floor; no health loss, kill or timer refresh.\n";
            Require(normalCaptured && readyCaptured,"Need both normal and finisher gameplay samples");
            frozenCamera=Camera.main.GetComponent<CameraController>();cameraEnabled=frozenCamera.enabled;frozenCamera.enabled=false;
            frozenAnimator=enemy.GetComponent<Animator>();animatorSpeed=frozenAnimator.speed;frozenAnimator.speed=0;
            Time.timeScale=0;frozen=true;frozenFrame=Time.frameCount;stage=4;
        }else if(stage==4 && Time.frameCount>frozenFrame){
            Capture("BodyFlinchHeld",false);
            typeof(EnemyHitReaction).GetField("elapsed",Private).SetValue(reaction,(float)typeof(EnemyHitReaction).GetField("recoveryTime",Private).GetValue(reaction));
            typeof(EnemyHitReaction).GetMethod("RemoveOffset",Private).Invoke(reaction,null);frozenFrame=Time.frameCount;stage=5;
        }else if(stage==5 && Time.frameCount>frozenFrame){
            Capture("BodyNeutralHeld",false);CheckRenderedBody();RestoreFrozen();input.active=false;player.GetComponent<TouchInputManager>().SetAim(false);stage=6;startTime=Time.time;
        }else if(stage==6 && Time.time-startTime>.8f){
            Require(reaction.AppliedJoltDegrees==0 && reaction.AppliedKickDistance==0 && reaction.Sparks.particleCount==0,"Live hit effects must recover and clear");
            Require(trafficSeen,"Moving combat check must reach actual traffic, not just the empty starting road");Finish(true,report+"PASS nearby moving traffic was present.\nPASS visible contacts recover cleanly and aim shutdown produces no muzzle-flash hierarchy error.\n");
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void RestoreFrozen(){if(!frozen)return;Time.timeScale=1;frozen=false;if(frozenAnimator!=null)frozenAnimator.speed=animatorSpeed;if(frozenCamera!=null)frozenCamera.enabled=cameraEnabled;}
    private static void CheckRenderedBody(){
        var a=new Texture2D(2,2);var b=new Texture2D(2,2);
        try{
            a.LoadImage(File.ReadAllBytes("Logs/EnemyHitLive/BodyFlinchHeld.png"));b.LoadImage(File.ReadAllBytes("Logs/EnemyHitLive/BodyNeutralHeld.png"));
            var x=a.GetPixels32();var y=b.GetPixels32();var camera=Camera.main;float previousAspect=camera.aspect;camera.aspect=1280f/720;var viewport=camera.WorldToViewportPoint(enemy.transform.position+Vector3.up*2.7f);camera.aspect=previousAspect;var center=new Vector2(viewport.x*1280,viewport.y*720);int changed=0;
            for(int row=Mathf.Max(0,(int)center.y-100);row<Mathf.Min(720,(int)center.y+100);row++)for(int col=Mathf.Max(0,(int)center.x-65);col<Mathf.Min(1280,(int)center.x+65);col++){
                int i=row*1280+col;if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>35)changed++;
            }
            Require(changed>=70,"Rendered body must visibly change across fresh skinned-mesh frames, changed pixels="+changed);report+="PASS rendered flinch/neutral comparison with world/camera held: "+changed+" changed enemy pixels.\n";
        }finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
    }
    private static void CheckGunAttachments(){
        var items=(System.Collections.IEnumerable)typeof(EnemyHitReaction).GetField("attachments",Private).GetValue(reaction);int count=0;
        foreach(var item in items){
            var type=item.GetType();var root=(Transform)type.GetField("root").GetValue(item);var grip=(Transform)type.GetField("target").GetValue(item);
            var before=(Vector3)type.GetField("worldPosition").GetValue(item);var targetBefore=(Vector3)type.GetField("targetPosition").GetValue(item);var rotationBefore=(Quaternion)type.GetField("targetRotation").GetValue(item);
            var original=Quaternion.Inverse(rotationBefore)*(before-targetBefore);var current=Quaternion.Inverse(grip.rotation)*(root.position-grip.position);
            Require(Vector3.Distance(original,current)<.002f,"Recoil must preserve the gun's authored offset from its hand target: "+root.name+" error="+Vector3.Distance(original,current)+" original="+original+" current="+current);count++;
        }
        Require(count==2,"Both enemy guns must follow the body flinch");
    }
    private static void Capture(string name,bool checkRecoil=true){
        var camera=Camera.main;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes=new RenderMode[canvases.Length];var cameras=new Camera[canvases.Length];var distances=new float[canvases.Length];
        var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{
            camera.targetTexture=rt;camera.aspect=1280f/720;
            for(int i=0;i<canvases.Length;i++){var canvas=canvases[i];modes[i]=canvas.renderMode;cameras[i]=canvas.worldCamera;distances[i]=canvas.planeDistance;if(canvas.renderMode==RenderMode.ScreenSpaceOverlay){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.05f;}}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/EnemyHitLive/"+name+".png",texture.EncodeToPNG());
            var sparkRenderer=reaction.Sparks.GetComponent<ParticleSystemRenderer>();sparkRenderer.enabled=false;
            try{
                camera.Render();RenderTexture.active=rt;var hidden=new Texture2D(1280,720,TextureFormat.RGB24,false);hidden.ReadPixels(new Rect(0,0,1280,720),0,0);hidden.Apply();var a=texture.GetPixels32();var b=hidden.GetPixels32();int changed=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>35)changed++;
                File.WriteAllBytes("Logs/EnemyHitLive/"+name+"NoSparks.png",hidden.EncodeToPNG());UnityEngine.Object.DestroyImmediate(hidden);Require(changed>=80,"Live aim contacts must visibly render sparks, pixels="+changed);report+="RENDER "+name+": spark pixels="+changed+", count="+reaction.Sparks.particleCount+", jolt="+reaction.AppliedJoltDegrees+"\n";
            }finally{sparkRenderer.enabled=true;}
            if(checkRecoil){
            Transform head=null;foreach(var bone in enemy.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.006"){head=bone;break;}
            Require(head!=null,"Need the actual rendered head bone to measure flinch visibility");
            var withJolt=camera.WorldToScreenPoint(head.position);var rootBefore=enemy.transform.position;
            typeof(EnemyHitReaction).GetMethod("ApplyRenderPose",Private).Invoke(reaction,new object[]{camera});CheckGunAttachments();typeof(EnemyHitReaction).GetMethod("RemoveOffset",Private).Invoke(reaction,null);
            try{
                var withoutJolt=camera.WorldToScreenPoint(head.position);float pixels=Vector2.Distance(withJolt,withoutJolt);
                Require(pixels>=3,"Body flinch must move the visible head from the ordinary aim camera, pixels="+pixels);
                Require(enemy.transform.position==rootBefore,"Visual recoil cannot displace enemy navigation");
                report+="RECOIL "+name+": visible head displacement="+pixels+" pixels; gun/hand offsets preserved.\n";
            }finally{typeof(EnemyHitReaction).GetMethod("LateUpdate",Private).Invoke(reaction,null);}
            }
            var samples=new ParticleSystem.Particle[192];int count=reaction.Sparks.GetParticles(samples);string details=name+" root="+enemy.transform.position+" contact="+reaction.LastContactPoint+" bounds="+sparkRenderer.bounds+"\n";
            for(int i=0;i<Mathf.Min(count,8);i++){var world=reaction.Sparks.transform.TransformPoint(samples[i].position);details+="p="+world+" screen="+camera.WorldToScreenPoint(world)+" size="+samples[i].GetCurrentSize3D(reaction.Sparks)+" life="+samples[i].remainingLifetime+"\n";}
            File.AppendAllText("Logs/EnemyHitLive/RenderMetrics.txt",details);

        }finally{
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
            camera.targetTexture=oldTarget;camera.ResetAspect();RenderTexture.active=oldActive;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);Canvas.ForceUpdateCanvases();
        }
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;RestoreFrozen();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyHitLive/Result.txt",message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
