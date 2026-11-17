using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.UI;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in the isolated validation project, with the real player, enemy and effects.
[InitializeOnLoad]
public static class FullDistanceFinisherChecks {
    private const string Key="CyberBiker.FullDistanceFinisherChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput : IPlayerInput {
        public Vector2 Steering=>Vector2.zero;
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>false;
    }
    private static GameplayManager game;
    private static PlayerController player;
    private static BikeSwordCombat sword;
    private static SwordFinisherEffects effects;
    private static Rigidbody body;
    private static Enemy victim,waiting;
    private static int stage,pass;
    private static bool done,sawDash,sawDrawingMovement;
    private static float stageTime,victimZ,drawStartZ;
    private static int launchCount;
    private static double began;
    static FullDistanceFinisherChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/FullDistanceFinisher");File.WriteAllText("Logs/FullDistanceFinisher/Result.txt","");
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(TestingSettings.PreferenceKey));
        SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(TestingSettings.PreferenceKey,""));
        PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Report(string value){File.AppendAllText("Logs/FullDistanceFinisher/Result.txt","PASS "+value+"\n");}
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
    private static void Next(int value){stage=value;stageTime=Time.unscaledTime;}
    private static Enemy Spawn(float x,float z){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab");
        var go=UnityEngine.Object.Instantiate(prefab,body.position+new Vector3(x,0,z),Quaternion.identity);
        go.name="Full-distance finisher fixture";var enemy=go.GetComponent<Enemy>();
        Set(enemy,"health",100);Set(enemy,"shootingDamage",0f);Set(enemy,"finisherWindow",12f);Set(enemy,"speed",110f);
        Set(enemy,"path",new AI.DodgePoint(go.transform.position+Vector3.forward*10000,go));
        Set(enemy,"followTarget",go.transform.position+Vector3.forward*10000);
        return enemy;
    }
    private static void Place(Enemy enemy,float x,float z){
        enemy.transform.position=body.position+new Vector3(x,0,z);enemy.GetComponent<Rigidbody>().position=enemy.transform.position;
        Set(enemy,"followTarget",enemy.transform.position+Vector3.forward*10000);Physics.SyncTransforms();
    }
    private static void Tick(){
        if(done || !EditorApplication.isPlaying)return;
        try {
            Require(EditorApplication.timeSinceStartup-began<90,"Finisher check timed out at stage "+stage);
            if(Time.timeSinceLevelLoad<.7f)return;
            if(stage==0){
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();game=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
                sword=player.GetComponent<BikeSwordCombat>();effects=player.GetComponent<SwordFinisherEffects>();body=player.GetComponent<Rigidbody>();
                foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
                foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
                foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None)){spawner.StopAllCoroutines();spawner.enabled=false;}
                foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None)){director.StopAllCoroutines();director.enabled=false;}
                game.SwitchGameState(GameState.Playing);player.GetComponent<TouchInputManager>().enabled=false;
                Set(player,"input",new NeutralInput());Set(sword,"launchKey",KeyCode.None);
                body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                body.position=new Vector3(0,100,0);player.transform.position=body.position;body.linearVelocity=Vector3.forward*110;
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
                victim=Spawn(-6,80);waiting=Spawn(6,130);Next(1);
            }else if(stage==1 && victim.IsAlive && waiting.IsAlive){
                Require(!sword.IsInFinisherRange(victim),"A healthy enemy must not be finishable");
                Require(player.RequestPistolTap() && player.HasBufferedPistolTap && !sword.IsActive,"Shoot must stay a pistol tap when no glowing enemy exists");player.CancelPistolTaps();
                sword.Launch();Require(sword.Target==null && sword.Phase==BikeSwordCombat.CombatPhase.Pursuing,"Healthy enemies must not start a dash");
                sword.Launch();Set(sword,"nextLaunch",0f);
                victim.TakeDamage(new DamageInfo(75,player.gameObject));waiting.TakeDamage(new DamageInfo(75,player.gameObject));
                Place(victim,-6,80);Place(waiting,6,130);
                Require(victim.IsFinisherReady && waiting.IsFinisherReady,"Both targets must be glowing");
                Require(sword.IsInFinisherRange(victim),"An 80m glowing enemy must be reachable without riding closer");
                launchCount=sword.LaunchCount;
                Set(sword,"launchCharge",0f);Require(!sword.CanStartLaunch,"Empty-meter fixture must block manual Launch");
                Require(player.RequestPistolTap(),"Shoot must accept a finisher tap");Require(sword.Target==victim && sword.Phase==BikeSwordCombat.CombatPhase.Drawing,"Shoot must commit to the distant enemy immediately, without Launch");
                Require(sword.IsDashing && !sword.IsPursuing && sword.LaunchCount==launchCount,"Shoot must start the dash with the draw, without activating Launch");
                Require(!player.HasBufferedPistolTap && !player.TapPistolReady,"A finisher tap must not also queue a pistol shot");
                Require(player.RequestPistolTap() && sword.Phase==BikeSwordCombat.CombatPhase.Drawing && sword.Target==victim,"Repeated Shoot during drawing must not cancel the finisher");
                victimZ=victim.transform.position.z;drawStartZ=body.position.z;sawDash=sawDrawingMovement=false;Next(2);
            }else if(stage==2){
                Require(game.gameState==GameState.Playing,"A finisher must not crash the player");
                Require(!sword.IsPursuing && sword.LaunchCount==launchCount && sword.BoostBlend==0,"A direct finisher must not enter Launch or ramp its speed boost");
                if(sword.Phase==BikeSwordCombat.CombatPhase.Drawing && body.position.z>drawStartZ+5)sawDrawingMovement=true;
                if(sword.IsDashing){sawDash=true;Require(sword.LastPickupDistance>50,"Dash must begin far beyond the old 14m pickup range");}
                if(sword.HitCount>pass){
                    Require(sawDash && sawDrawingMovement && !victim.IsAlive,"The real long-distance dash must move during drawing and execute its enemy");
                    Require(!sword.IsPursuing && sword.AllowsAftermathSlowMotion,"The kill must stop Launch but preserve aftermath slow motion");
                    Require(waiting.IsAlive && waiting.IsFinisherReady,"The second glowing enemy must be left alive");
                    Require(victim.transform.position.z-victimZ>-1,"The enemy must not be teleported backwards to the bike");
                    Require(effects.ImpactCount==pass+1 && effects.LastImpactParticleCount>0,"The actual sword impact must still play");
                    Require(sword.LastAttackSide==(pass==0 ? BikeSwordCombat.AttackSide.Left : BikeSwordCombat.AttackSide.Right),"Full-distance finishers must choose the correct side");
                    Report((pass==0 ? "80m left" : "120m right")+" target: Shoot tap starts immediate draw, full-distance dash, one-shot kill, correct swing, live particles and Launch off at contact; no buffered pistol shot.");
                    Next(3);
                }else Require(Time.unscaledTime-stageTime<5,"Distant dash failed to land; phase="+sword.Phase+" gap="+Vector3.Distance(body.position,victim.transform.position));
            }else if(stage==3){
                Require(!sword.IsPursuing && sword.HitCount==pass+1,"Launch must never restart and auto-chain after the kill");
                if(effects.CarriedExplosionCount>pass && !sword.IsActive && game.CombatSlowMotionRemaining<=0){
                    Require(waiting.IsAlive,"Stopping Launch must prevent the waiting enemy being finished");
                    Require(sword.BoostBlend<.01f,"Launch speed boost must fade away after the kill");
                    Report("sword returns to riding, carried explosion completes, boost fades, and another glowing enemy is not auto-finished.");
                    pass++;
                    if(pass==1){
                        victim=waiting;waiting=Spawn(-6,130);Place(victim,6,120);Next(4);
                    }else {
                        victim=waiting;Place(victim,0,70);Next(5);
                    }
                }else Require(Time.unscaledTime-stageTime<10,"Finisher recovery or explosion stalled");
            }else if(stage==4 && waiting.IsAlive){
                waiting.TakeDamage(new DamageInfo(75,player.gameObject));Place(waiting,-6,140);
                victim.TakeDamage(new DamageInfo(75,player.gameObject));Place(victim,6,120);Set(sword,"nextLaunch",0f);
                launchCount=sword.LaunchCount;
                Set(sword,"launchCharge",0f);Require(!sword.CanStartLaunch,"Empty-meter fixture must block manual Launch");
                Require(player.GetComponent<TouchInputManager>().TapPistol(),"The actual shared fire-button route must accept a finisher tap");Require(sword.Phase==BikeSwordCombat.CombatPhase.Drawing && sword.Target==victim,"Right target must start from 120m by Shoot, without Launch");
                Require(sword.IsDashing && !sword.IsPursuing && sword.LaunchCount==launchCount,"Phone Shoot must start drawing and dashing together without Launch");
                victimZ=victim.transform.position.z;drawStartZ=body.position.z;sawDash=sawDrawingMovement=false;Next(2);
            }else if(stage==5 && victim.IsAlive){
                victim.TakeDamage(new DamageInfo(75,player.gameObject));Place(victim,0,70);
                // Range and walls still bound valid targets; no across-world or through-wall teleport.
                Set(sword,"targetRange",50f);Require(!sword.IsInFinisherRange(victim),"Configured target range must still apply");Set(sword,"targetRange",150f);
                var wall=new GameObject("Finisher wall check");wall.transform.position=body.position+new Vector3(0,1.5f,35);wall.AddComponent<BoxCollider>().size=new Vector3(20,5,1);Physics.SyncTransforms();
                Set(sword,"nextLaunch",0f);sword.Launch();Require(sword.Phase==BikeSwordCombat.CombatPhase.Pursuing,"A solid wall must prevent commitment");
                UnityEngine.Object.DestroyImmediate(wall);sword.Launch();Set(sword,"nextLaunch",0f);sword.Launch();
                Require(sword.Phase==BikeSwordCombat.CombatPhase.Drawing,"Clearing the wall must permit the dash");Set(victim,"finisherRemaining",.001f);Next(6);
            }else if(stage==6 && Time.unscaledTime-stageTime>1.5f){
                Require(victim.IsAlive && !victim.IsFinisherReady && sword.HitCount==2,"Expiry during the draw must cancel without killing");
                Report("healthy targets rejected; configured reach and solid walls respected; expiry during draw cancels without a false kill.");
                Finish(true,"PASS full-distance finisher integration checks.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Finish(bool success,string message){
        if(done)return;done=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;
        if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        File.AppendAllText("Logs/FullDistanceFinisher/Result.txt",message+"\n");Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
