using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Audio;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[InitializeOnLoad]
public static class EnemyFinisherChecks {
    private sealed class Input : IPlayerInput {
        private readonly TouchInputManager touch;
        public Input(TouchInputManager value){touch=value;}
        public Vector2 Steering=>Vector2.zero;
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>touch.aim;
    }
    private const string Pending="CyberBiker.EnemyFinisherChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static GameplayManager manager;
    private static BikeSwordCombat sword;
    private static Enemy template,enemy,otherEnemy,expiring;
    private static Rigidbody body;
    private static TouchInputManager touch;
    private static int stage,enemyShots;
    private static float stageTime,startingGap,pausedWindow;
    private static bool distanceRetuned;
    private static bool warningPauseStarted,warningPauseFinished;
    private static float readyPreviewStarted=-1;
    private static Material observedHighlight;
    private static bool? lastOrange;
    private static float lastSwitchTime;
    private static bool sawBlue,sawOrange,orangeCaptured;
    private static readonly List<float> switchIntervals=new List<float>();
    private static double pauseClock;
    private static bool finishing;
    private static string report="";
    private static readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
    static EnemyFinisherChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FinisherHighlight.shader");
        if(shader==null || ShaderUtil.ShaderHasError(shader)){Debug.LogError("Finisher shader failed validation");EditorApplication.Exit(1);return;}
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Attach(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Next(){stage++;stageTime=Time.time;}
    private static Enemy Spawn(float x,float z){
        var go=UnityEngine.Object.Instantiate(template.gameObject,player.transform.position+new Vector3(x,0,z),Quaternion.identity);go.name="Finisher mechanics test enemy";
        var result=go.GetComponent<Enemy>();typeof(Enemy).GetField("health",Private).SetValue(result,100);typeof(Enemy).GetField("speed",Private).SetValue(result,110f);
        typeof(Enemy).GetField("shootingDamage",Private).SetValue(result,0f);
        typeof(Enemy).GetField("path",Private).SetValue(result,new AI.DodgePoint(go.transform.position+Vector3.forward*10000,go));
        typeof(Enemy).GetField("followTarget",Private).SetValue(result,go.transform.position+Vector3.forward*10);
        go.SetActive(true);return result;
    }
    private static void Tick(){
        if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;
        try {
            if(stage>=3 && stage<=5 && enemy!=null && enemy.IsFinisherReady)ObserveFlashes();
            if(stage==0){
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();sword=player.GetComponent<BikeSwordCombat>();body=player.GetComponent<Rigidbody>();touch=player.GetComponent<TouchInputManager>();
                foreach(var item in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)){if(template==null)template=item;item.gameObject.SetActive(false);}
                foreach(var item in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))item.gameObject.SetActive(false);
                foreach(var item in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))item.enabled=false;
                manager.SwitchGameState(GameState.Playing);touch.enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new Input(touch));typeof(BikeSwordCombat).GetField("launchKey",Private).SetValue(sword,KeyCode.None);
                player.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.identity);body.position=player.transform.position;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;body.linearVelocity=Vector3.forward*110;
                manager.EnemyDistanceFromPlayer=15;
                enemy=Spawn(3.5f,45);otherEnemy=Spawn(-3.5f,55);Next();
            } else if(stage==1 && enemy.IsAlive && otherEnemy.IsAlive && Time.time-stageTime>2){
                Require(Mathf.Abs(Vector3.Distance(enemy.transform.position,body.position)-15)<.6f && Mathf.Abs(Vector3.Distance(otherEnemy.transform.position,body.position)-15)<.6f,"Both enemies must approach the global distance during normal riding");
                report+="PASS global distance: two enemies approach the shared 15m setting during normal riding, overriding their legacy per-enemy values.\n";
                foreach(var item in enemy.GetComponentsInChildren<Renderer>(true))if(item.name!="Finisher glow")originals[item]=item.sharedMaterials;
                Require(!enemy.IsFinisherReady && !enemy.DefeatFromSword(player.gameObject),"A healthy enemy cannot be sword-finished");
                sword.Launch();startingGap=enemy.transform.position.z-player.transform.position.z;Next();
            } else if(stage==2){
                Require(enemy.IsAlive && !enemy.IsFinisherReady && sword.HitCount==0 && sword.DrawCompletionCount==0 && sword.Target==null,"Launch must not acquire, draw for or kill healthy enemies");
                float distance=Vector3.Distance(enemy.transform.position,body.position);
                if(!distanceRetuned && Time.time-stageTime>2) {
                    Require(Mathf.Abs(distance-15)<.6f,"Launch must close to the configured 15m, rather than locking the starting gap; distance="+distance);
                    report+="PASS configured Launch distance: approached 15m from "+startingGap.ToString("F1")+"m; measured "+distance.ToString("F3")+"m.\n";
                    Require(Mathf.Abs(Vector3.Distance(otherEnemy.transform.position,body.position)-15)<.6f,"All healthy enemies must keep the same global gap during Launch");
                    manager.EnemyDistanceFromPlayer=25;distanceRetuned=true;stageTime=Time.time;
                } else if(distanceRetuned && Time.time-stageTime>1.5f) {
                    Require(Mathf.Abs(distance-25)<.6f,"Live Inspector distance edits must change the Launch setpoint; distance="+distance);
                    Require(Mathf.Abs(Vector3.Distance(otherEnemy.transform.position,body.position)-25)<.6f,"A single global edit must retune both live enemies");
                    Require(Mathf.Abs(enemy.ForwardSpeed-body.linearVelocity.z)<3,"Enemy must match player speed after reaching the distance setpoint");
                    otherEnemy.gameObject.SetActive(false);
                    report+="PASS live global distance control: both enemies reach 25m after one edit; healthy sword targeting and damage remain blocked.\n";touch.SetAim(true);Next();
                }
            } else if(stage==3){
                if(enemy.IsFinisherReady && enemy.HighlightRendererCount>0){
                    touch.SetAim(false);if(readyPreviewStarted<0)readyPreviewStarted=Time.time;
                    if(Time.time-readyPreviewStarted<.5f || enemy.FinisherHighlightColor.r>enemy.FinisherHighlightColor.b)return;
                    Require(enemy.IsAlive && enemy.CurrentHealth<=30 && enemy.FinisherTimeRemaining>3,"Actual pistol fire must stagger a living enemy");
                    Require(GameAudio.Active.FinisherReadyCueCount==1 && GameAudio.Active.FinisherWarningCueCount==0,"Entry must play exactly one ready chime, with no early warning");
                    CheckMaterials();Capture("ready-cyan",enemy);Capture("ready-cyan-chase",enemy,true);
                    float remaining=enemy.FinisherTimeRemaining;enemy.TakeDamage(new DamageInfo(10000,player.gameObject));
                    Require(enemy.IsAlive && enemy.CurrentHealth>0 && Mathf.Approximately(remaining,enemy.FinisherTimeRemaining),"Overkill pistol damage must not kill or refresh the window");
                    Require(GameAudio.Active.FinisherReadyCueCount==1,"Repeated damage must not replay the ready chime");
                    pausedWindow=enemy.FinisherTimeRemaining;manager.SwitchGameState(GameState.Pause);pauseClock=EditorApplication.timeSinceStartup;Next();
                } else Require(Time.time-stageTime<8,"Pistol failed to reach finisher state; health="+enemy.CurrentHealth);
            } else if(stage==4){
                Require(Mathf.Approximately(enemy.FinisherTimeRemaining,pausedWindow),"Pause must freeze the finisher window");
                Require(GameAudio.Active.FinisherWarningCueCount==0,"Pause must not advance toward a warning");
                if(EditorApplication.timeSinceStartup-pauseClock>.25){manager.SwitchGameState(GameState.Playing);report+="PASS real pistol stagger, nonlethal overkill, no hit-based window refresh, pause-safe countdown and visible blue highlight.\n";Next();}
            } else if(stage==5){
                if(GameAudio.Active.FinisherWarningCueCount>0 && !warningPauseFinished){
                    if(!warningPauseStarted){
                        Require(enemy.FinisherTimeRemaining>0 && enemy.FinisherTimeRemaining<=1.3f,"Warning must start near expiry, not at entry");
                        var alert=GameAudio.Active.GetComponentsInChildren<AudioSource>().FirstOrDefault(s=>s.clip!=null && s.clip.name=="FinisherWarning" && s.isPlaying);
                        Require(alert!=null && alert.spatialBlend==0 && alert.volume>.8f && alert.priority==24,"Warning must play a bound, audible listener-relative clip with priority");
                        pausedWindow=enemy.FinisherTimeRemaining;pauseClock=EditorApplication.timeSinceStartup;warningPauseStarted=true;manager.SwitchGameState(GameState.Pause);
                        Require(!alert.isPlaying,"Pause must suspend the warning voice");
                    }
                    Require(Mathf.Approximately(pausedWindow,enemy.FinisherTimeRemaining) && GameAudio.Active.FinisherWarningCueCount==1,"Warning and countdown must remain frozen without retriggering");
                    if(EditorApplication.timeSinceStartup-pauseClock<.25)return;
                    manager.SwitchGameState(GameState.Playing);warningPauseFinished=true;
                }
                if(enemy.IsFinisherReady && !orangeCaptured && enemy.FinisherHighlightColor.r>enemy.FinisherHighlightColor.b){Capture("ready-orange",enemy);Capture("ready-orange-chase",enemy,true);orangeCaptured=true;}
                if(!enemy.IsFinisherReady){
                    Require(warningPauseFinished && GameAudio.Active.FinisherReadyCueCount==1 && GameAudio.Active.FinisherWarningCueCount==1,"One full window must play one entry and one warning, including pause/resume");
                    report+="PASS finisher audio: bound entry chime once, no retrigger on extra hits, one warning at 1.25s, listener-relative priority voice and paused countdown/voice.\n";
                    Require(enemy.IsAlive && enemy.CurrentHealth>=59 && enemy.HighlightRendererCount==0,"Expired window must restore fighting health and remove the glow");
                    Require(sawBlue && sawOrange && orangeCaptured && switchIntervals.Count>=8,"The actual highlight material must repeatedly alternate blue and orange while far away");
                    float early=switchIntervals.Take(3).Average(),late=switchIntervals.TakeLast(3).Average();
                    Require(late<early*.7f,"Color changes must accelerate toward expiry; early="+early+" late="+late);
                    report+="PASS timed color alternation: actual material changes blue/orange at distance; switch interval accelerates from "+early.ToString("F3")+"s to "+late.ToString("F3")+"s near expiry.\n";
                    enemyShots=enemy.FiredShotCount;CheckMaterials();Next();
                }
                else Require(Time.time-stageTime<6,"Finisher window never expired");
            } else if(stage==6 && Time.time-stageTime>1.1f){
                Require(enemy.FiredShotCount>enemyShots,"Recovered enemy must resume actual gunfire");
                Require(!enemy.DefeatFromSword(player.gameObject),"Recovered enemy must reject sword finishers again");
                enemy.TakeDamage(new DamageInfo(15,player.gameObject));Require(!enemy.IsFinisherReady,"One hit after recovery must not instantly re-stagger");enemy.TakeDamage(new DamageInfo(15,player.gameObject));Require(enemy.IsFinisherReady,"Further damage must create another finite finisher window; health="+enemy.CurrentHealth.ToString("F6"));
                enemy.transform.SetPositionAndRotation(player.transform.position+new Vector3(-3.5f,0,10),Quaternion.identity);
                typeof(Enemy).GetField("followTarget",Private).SetValue(enemy,enemy.transform.position+Vector3.forward*10);typeof(Enemy).GetField("path",Private).SetValue(enemy,new AI.DodgePoint(enemy.transform.position+Vector3.forward*10000,enemy.gameObject));
                Next();
            } else if(stage==7 && Time.time-stageTime>.55f && enemy.FinisherHighlightColor.b>enemy.FinisherHighlightColor.r){
                Require(enemy.FinisherInRange && enemy.HighlightRendererCount>0,"Close finisher-ready target must show the in-range cue");
                Require(enemy.FinisherHighlightColor.b>enemy.FinisherHighlightColor.r,"A nearby target must still alternate to blue; range must not choose its color");Capture("ready-close-blue",enemy);
                enemy.transform.position=player.transform.position+new Vector3(-3.5f,0,30);typeof(Enemy).GetField("followTarget",Private).SetValue(enemy,enemy.transform.position+Vector3.forward*10);
                sword.Launch();report+="PASS expiry: enemy recovers, loses highlight, resumes firing and becomes immune to the sword until staggered again; close-range blue phase also verified.\n";Next();
            } else if(stage==8){
                if(sword.HitCount==1){Require(!enemy.IsAlive && enemy.HighlightRendererCount==0,"Sword must execute and clear the ready target's glow");Next();}
                else Require(Time.time-stageTime<4,"Launch could not catch and finish the staggered enemy");
            } else if(stage==9 && Time.time-stageTime>.4f){
                Require(GameAudio.Active.FinisherReadyCueCount==2 && GameAudio.Active.FinisherWarningCueCount==1,"A new window must rearm entry; an early sword kill must not warn later");
                Require(player.GetComponent<SwordFinisherEffects>().CarriedExplosionCount>=1,"Accepted finisher must produce the cinematic moving explosion");
                touch.SetAim(true);touch.SetAim(false);typeof(BikeSwordCombat).GetField("nextLaunch",Private).SetValue(sword,0f);
                expiring=Spawn(0,20);Next();
            } else if(stage==10 && expiring.IsAlive){
                Require(Mathf.Approximately(expiring.playerDistance,25),"A newly spawned enemy must inherit the current global distance");
                report+="PASS newly spawned enemy uses the edited global 25m distance without per-enemy setup.\n";
                expiring.TakeDamage(new DamageInfo(75,player.gameObject));Require(expiring.IsFinisherReady,"Expiry-race fixture must be ready");sword.Launch();Next();
            } else if(stage==11){
                if(sword.Phase==BikeSwordCombat.CombatPhase.Drawing && sword.Target==expiring){typeof(Enemy).GetField("finisherRemaining",Private).SetValue(expiring,.001f);Next();}
                else Require(Time.time-stageTime<3,"Could not reach pre-dash expiry case");
            } else if(stage==12 && Time.time-stageTime>.7f){
                Require(GameAudio.Active.FinisherReadyCueCount==3 && GameAudio.Active.FinisherWarningCueCount==1,"Expiry without a remaining opportunity must not emit a stale warning");
                Require(expiring.IsAlive && !expiring.IsFinisherReady && sword.HitCount==1 && sword.Phase!=BikeSwordCombat.CombatPhase.Striking,"Expiry during draw must cancel the pickup without a false kill or stalled attack");
                report+="PASS ready target: becomes catchable, sword kill/explosion succeed, and expiry during preparation cancels safely.\n";Finish(true,report);
            }
        }catch(Exception error){Finish(false,error+"\n"+report);}
    }
    private static void ObserveFlashes(){
        if(observedHighlight==null){var renderer=enemy.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Finisher glow" && r.enabled && r.gameObject.activeInHierarchy);if(renderer==null)return;observedHighlight=renderer.sharedMaterial;}
        var color=observedHighlight.GetColor("_HighlightColor");bool orange=color.r>color.b;sawOrange|=orange;sawBlue|=!orange;
        if(lastOrange.HasValue && lastOrange.Value!=orange){switchIntervals.Add(Time.time-lastSwitchTime);lastSwitchTime=Time.time;}
        else if(!lastOrange.HasValue)lastSwitchTime=Time.time;
        lastOrange=orange;
    }
    private static void CheckMaterials(){foreach(var item in originals)if(item.Key!=null)Require(item.Key.sharedMaterials.SequenceEqual(item.Value),"Highlight must preserve original materials");}
    private static void Capture(string name,Enemy target,bool chaseDistance=false){
        var go=new GameObject("Finisher visual review camera");var view=go.AddComponent<Camera>();view.transform.position=target.transform.position+(chaseDistance ? new Vector3(2,2,-25) : new Vector3(5,4,-8));view.transform.LookAt(target.transform.position+Vector3.up*2);view.fieldOfView=chaseDistance ? 50 : 45;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.025f,.03f,.04f);
        var production=Camera.main.GetComponent<PostProcessLayer>();Require(production!=null,"Preview must use the production post-processing layer");
        var resources=new SerializedObject(production).FindProperty("m_Resources").objectReferenceValue as PostProcessResources;
        var effects=go.AddComponent<PostProcessLayer>();effects.Init(resources);effects.volumeLayer=production.volumeLayer;effects.volumeTrigger=view.transform;view.allowHDR=true;
        var rt=new RenderTexture(900,700,24,RenderTextureFormat.DefaultHDR);var previous=RenderTexture.active;view.targetTexture=rt;view.Render();RenderTexture.active=rt;var image=new Texture2D(900,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,700),0,0);image.Apply();Directory.CreateDirectory("Logs/EnemyFinisher");File.WriteAllBytes("Logs/EnemyFinisher/"+name+".png",image.EncodeToPNG());
        var bloom=effects.GetSettings<Bloom>();Require(bloom.enabled.value && bloom.intensity.value>10,"Visual preview must include the game's strong bloom");
        report+="Visual preview "+name+": production bloom intensity "+bloom.intensity.value.ToString("F2")+", threshold "+bloom.threshold.value.ToString("F2")+".\n";
        RenderTexture.active=previous;view.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
    }
    private static void Finish(bool passed,string result){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Directory.CreateDirectory("Logs/EnemyFinisher");File.WriteAllText("Logs/EnemyFinisher/result.txt",result);EditorApplication.Exit(passed?0:1);}
}
